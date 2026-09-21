// UNCOMPILED: this file has never been built by a Unity Editor. See
// game/Assets/Scripts/Runtime/README.md and game/README.md before
// trusting anything in it.
using System;
using System.IO;
using Thaivia.Core.MapPack;
using Thaivia.Core.Serialization;
using Thaivia.Core.Simulation;
using UnityEngine;

namespace Thaivia.Runtime
{
    /// <summary>
    /// Loads a MapPack file (via the pure Thaivia.Core.Serialization.MapPackLoader
    /// -- this behaviour adds no parsing/validation logic of its own, only
    /// the Unity lifecycle glue) and publishes the resulting WorldState for
    /// the rendering/selection/UI layers to read.
    /// </summary>
    public sealed class MapPackLoaderBehaviour : MonoBehaviour
    {
        [Tooltip("Absolute or StreamingAssets-relative path to a *.mappack.json file.")]
        [SerializeField] private string mapPackPath = string.Empty;

        [Tooltip("Fired once the MapPack has been loaded and validated.")]
        public event Action<WorldState>? Loaded;

        [Tooltip("Fired when loading fails (bad path, tampered file, unsupported version, ...).")]
        public event Action<Exception>? LoadFailed;

        public WorldState? CurrentWorld { get; private set; }
        public MapPackDocument? CurrentDocument { get; private set; }

        private void Start()
        {
            if (!string.IsNullOrEmpty(mapPackPath))
            {
                Load(mapPackPath);
            }
        }

        public void Load(string path)
        {
            try
            {
                var resolvedPath = ResolvePath(path);
                var document = MapPackLoader.LoadFile(resolvedPath);
                CurrentDocument = document;

                if (document.Payload.Provenance.Synthetic)
                {
                    Debug.LogWarning(
                        $"MapPack '{document.Payload.Manifest.MapId}' is SYNTHETIC "
                        + $"({document.Payload.Provenance.SyntheticNotice}). It must never be "
                        + "presented to the player as a real place.");
                }

                CurrentWorld = new WorldState(document.Payload.GeographyBase, document.Payload.SimulationInitialization);
                Loaded?.Invoke(CurrentWorld);
            }
            catch (Exception ex)
            {
                // Never swallow a load failure and pretend an empty scene
                // is a valid MapPack -- AGENTS.md rule 6 ("CLI must not
                // fake success") applies here just as much as to the CLI.
                Debug.LogError($"MapPack load FAILED for '{path}': {ex.Message}");
                LoadFailed?.Invoke(ex);
            }
        }

        private static string ResolvePath(string path) =>
            Path.IsPathRooted(path) ? path : Path.Combine(Application.streamingAssetsPath, path);
    }
}
