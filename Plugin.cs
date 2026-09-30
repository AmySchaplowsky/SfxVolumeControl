using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace SfxVolumeControl
{
    public enum ScaleMethod
    {
        Both,
        AudioSourceOnly,
        ZsfxRangeOnly
    }

    [BepInPlugin(GUID, "SFX Volume Control", "0.2.0")]
    public class Plugin : BaseUnityPlugin
    {
        public const string GUID = "local.sfxvolumecontrol";

        private ConfigEntry<float> _master;
        private ConfigEntry<ScaleMethod> _method;
        private ConfigEntry<bool> _affectMusic;

        private class Group
        {
            public string Key;
            public ConfigEntry<float> Vol;
            public readonly List<ZSFX> Zsfx = new List<ZSFX>();
            public float Applied = -1f;
            public ScaleMethod AppliedMethod;
        }

        private readonly Dictionary<string, Group> _groups = new Dictionary<string, Group>();
        private readonly Dictionary<AudioSource, float> _srcBase = new Dictionary<AudioSource, float>();
        private readonly Dictionary<ZSFX, float[]> _zBase = new Dictionary<ZSFX, float[]>();

        private const BindingFlags AnyInstance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private static readonly FieldInfo MinVol = typeof(ZSFX).GetField("m_minVol", AnyInstance);
        private static readonly FieldInfo MaxVol = typeof(ZSFX).GetField("m_maxVol", AnyInstance);

        // Layer 2: looping / world sounds (torch flames, portal hum, ...) on live objects, adjusted every frame.
        private class Tracked
        {
            public AudioSource Src;
            public ConfigEntry<float> Vol;
            public float Base;
            public float LastSet = -1f;
            public bool MutedByUs;
        }
        private readonly List<Tracked> _world = new List<Tracked>();
        private readonly HashSet<AudioSource> _worldKnown = new HashSet<AudioSource>();
        private readonly Dictionary<string, ConfigEntry<float>> _worldVol = new Dictionary<string, ConfigEntry<float>>();

        private float _nextScan, _nextApply, _nextWorldScan;

        private void Awake()
        {
            _master = Config.Bind("Master", "Volume", 1f,
                new ConfigDescription("Multiplier applied on top of every individual sound volume. 0 = silent, 1 = default.",
                    new AcceptableValueRange<float>(0f, 1f)));
            _method = Config.Bind("Master", "ScaleMethod", ScaleMethod.Both,
                "How volume is changed. Both = AudioSource volume and the sound component's volume range. " +
                "If sounds get quieter than the slider suggests, try AudioSourceOnly or ZsfxRangeOnly.");

            _affectMusic = Config.Bind("Master", "AffectMusic", false, "Also apply volumes to music. Off by default; music is left alone.");

            if (MinVol == null || MaxVol == null)
                Logger.LogWarning("ZSFX volume fields (m_minVol/m_maxVol) not found. Only AudioSource volume will be changed.");
            Logger.LogInfo("Loaded. Individual sound volumes appear in the config after a world is loaded.");
        }

        private void Update()
        {
            float t = Time.unscaledTime;
            if (t >= _nextScan) { _nextScan = t + 5f; Scan(); }
            if (t >= _nextApply) { _nextApply = t + 0.25f; ApplyAll(); }
            if (t >= _nextWorldScan) { _nextWorldScan = t + 0.25f; RefreshWorld(); }
        }

        // Runs after the game's own sound code each frame so our volume is the last word.
        private void LateUpdate()
        {
            UpdateWorld();
        }

        // ---------- layer 2: sounds on live objects ----------

        private void RefreshWorld()
        {
            _worldKnown.RemoveWhere(s => s == null);
            int added = 0;
            bool prevSave = Config.SaveOnConfigSet;
            Config.SaveOnConfigSet = false;
            try
            {
                foreach (var src in FindObjectsOfType<AudioSource>())
                {
                    if (src == null || _worldKnown.Contains(src)) continue;
                    _worldKnown.Add(src);

                    // One-shot sounds made by the game's sound component are handled per prefab (layer 1).
                    if (src.GetComponent<ZSFX>() != null && !src.loop) continue;

                    string mixer = src.outputAudioMixerGroup != null ? src.outputAudioMixerGroup.name : "none";
                    if (!_affectMusic.Value && mixer.ToLowerInvariant().Contains("music")) continue;

                    string key = WorldKey(src);
                    ConfigEntry<float> entry;
                    if (!_worldVol.TryGetValue(key, out entry))
                    {
                        entry = Config.Bind("World: " + CategoryFor(key), key, 1f,
                            new ConfigDescription("Volume for the sound(s) on '" + key + "'. 0 = silent, 1 = default.", new AcceptableValueRange<float>(0f, 1f)));
                        _worldVol[key] = entry;
                        added++;
                        Logger.LogInfo("World sound: " + key + " (clip=" + (src.clip != null ? src.clip.name : "null") +
                                       ", loop=" + src.loop + ", mixer=" + mixer + ")");
                    }
                    _world.Add(new Tracked { Src = src, Vol = entry });
                }
            }
            finally
            {
                Config.SaveOnConfigSet = prevSave;
            }
            if (added > 0) Config.Save();
        }

        private void UpdateWorld()
        {
            float master = Mathf.Clamp01(_master.Value);
            for (int i = _world.Count - 1; i >= 0; i--)
            {
                var t = _world[i];
                if (t.Src == null) { _world.RemoveAt(i); continue; }

                float eff = master * Mathf.Clamp01(t.Vol.Value);

                // If the game changed the volume since we last set it, that is the new base value.
                if (Mathf.Abs(t.Src.volume - t.LastSet) > 0.0001f) t.Base = t.Src.volume;
                float target = t.Base * eff;
                t.Src.volume = target;
                t.LastSet = target;

                bool mute = eff <= 0f;
                if (mute && !t.Src.mute) { t.Src.mute = true; t.MutedByUs = true; }
                else if (!mute && t.MutedByUs) { t.Src.mute = false; t.MutedByUs = false; }
            }
        }

        private static string WorldKey(AudioSource src)
        {
            var root = src.transform.root;
            string rootName = StripClone(root.name);
            string name = root.gameObject == src.gameObject ? rootName : rootName + "/" + StripClone(src.gameObject.name);
            foreach (char c in new[] { '=', '\n', '\t', '"', '\'', '[', ']' }) name = name.Replace(c, '_');
            return name;
        }

        private static string StripClone(string n)
        {
            return n.EndsWith("(Clone)") ? n.Substring(0, n.Length - 7).Trim() : n;
        }

        private static string CategoryFor(string key)
        {
            string n = key;
            int slash = n.IndexOf('/');
            if (slash >= 0) n = n.Substring(0, slash);
            string lower = n.ToLowerInvariant();
            foreach (var prefix in new[] { "piece_", "sfx_", "fx_" })
                if (lower.StartsWith(prefix)) { n = n.Substring(prefix.Length); break; }
            int us = n.IndexOf('_');
            return (us > 0 ? n.Substring(0, us) : n).ToLowerInvariant();
        }

        // Finds every sound-effect prefab (prefab assets only, not live objects) and gives each its own volume setting.
        private void Scan()
        {
            int added = 0;
            bool prevSave = Config.SaveOnConfigSet;
            Config.SaveOnConfigSet = false;
            try
            {
                foreach (var z in Resources.FindObjectsOfTypeAll<ZSFX>())
                {
                    if (z == null || z.gameObject.scene.IsValid()) continue;
                    var zsrc = z.GetComponent<AudioSource>();
                    if (zsrc != null && zsrc.loop) continue; // looping sounds are handled on live objects (layer 2)
                    string key = KeyFor(z);
                    Group g;
                    if (!_groups.TryGetValue(key, out g))
                    {
                        g = new Group { Key = key };
                        g.Vol = Config.Bind(SectionFor(key), key, 1f,
                            new ConfigDescription("Volume for " + key + ". 0 = silent, 1 = default.", new AcceptableValueRange<float>(0f, 1f)));
                        _groups[key] = g;
                        added++;
                    }
                    if (!g.Zsfx.Contains(z)) { g.Zsfx.Add(z); g.Applied = -1f; }
                }
            }
            finally
            {
                Config.SaveOnConfigSet = prevSave;
            }
            if (added > 0)
            {
                Config.Save();
                Logger.LogInfo("Discovered " + added + " new sound(s). Total: " + _groups.Count);
            }
        }

        private void ApplyAll()
        {
            float master = Mathf.Clamp01(_master.Value);
            foreach (var g in _groups.Values)
            {
                float eff = master * Mathf.Clamp01(g.Vol.Value);
                if (Mathf.Approximately(eff, g.Applied) && g.AppliedMethod == _method.Value) continue;
                g.Applied = eff;
                g.AppliedMethod = _method.Value;
                Apply(g, eff);
            }
        }

        private void Apply(Group g, float eff)
        {
            bool doSrc = _method.Value != ScaleMethod.ZsfxRangeOnly;
            bool doRange = _method.Value != ScaleMethod.AudioSourceOnly;

            foreach (var z in g.Zsfx)
            {
                if (z == null) continue;

                var src = z.GetComponent<AudioSource>();
                if (src != null)
                {
                    float b;
                    if (!_srcBase.TryGetValue(src, out b)) { b = src.volume; _srcBase[src] = b; }
                    src.volume = doSrc ? b * eff : b;
                }

                float[] zb;
                if (!_zBase.TryGetValue(z, out zb)) { zb = new[] { GetF(MinVol, z), GetF(MaxVol, z) }; _zBase[z] = zb; }
                SetF(MinVol, z, doRange ? zb[0] * eff : zb[0]);
                SetF(MaxVol, z, doRange ? zb[1] * eff : zb[1]);
            }
        }

        private static string KeyFor(ZSFX z)
        {
            var root = z.transform.root;
            string name = root.gameObject == z.gameObject ? z.gameObject.name : root.name + "/" + z.gameObject.name;
            // Config keys cannot contain these characters.
            foreach (char c in new[] { '=', '\n', '\t', '"', '\'', '[', ']' }) name = name.Replace(c, '_');
            return name;
        }

        // Groups sounds into sections by the first word after the sfx_/fx_ prefix, e.g. sfx_wood_blocked -> "Sounds: wood".
        private static string SectionFor(string key)
        {
            string n = key;
            int slash = n.IndexOf('/');
            if (slash >= 0) n = n.Substring(0, slash);
            string lower = n.ToLowerInvariant();
            foreach (var prefix in new[] { "sfx_", "vfx_", "fx_" })
                if (lower.StartsWith(prefix)) { n = n.Substring(prefix.Length); break; }
            int us = n.IndexOf('_');
            string first = us > 0 ? n.Substring(0, us) : n;
            return "Sounds: " + first.ToLowerInvariant();
        }

        private static float GetF(FieldInfo f, object o) { return f == null ? 1f : (float)f.GetValue(o); }
        private static void SetF(FieldInfo f, object o, float v) { if (f != null) f.SetValue(o, v); }
    }
}
