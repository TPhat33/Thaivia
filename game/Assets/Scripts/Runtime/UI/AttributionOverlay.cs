// UNCOMPILED: never built by a Unity Editor. See
// game/Assets/Scripts/Runtime/README.md.
using Thaivia.Core.MapPack;
using UnityEngine;
using UnityEngine.UI;

namespace Thaivia.Runtime.UI
{
    /// <summary>
    /// Always-on-screen OSM/ODbL attribution (ADR-0004; AGENTS.md rule on
    /// licensing). This is intentionally NOT a settings-menu item or a
    /// once-per-session splash -- `Attach` wires it to a persistent UI
    /// element that stays visible for as long as a MapPack with
    /// non-empty attribution is loaded.
    /// </summary>
    public sealed class AttributionOverlay : MonoBehaviour
    {
        [SerializeField] private Text? attributionText;

        public void Attach(Attribution attribution)
        {
            if (attributionText == null)
            {
                Debug.LogError("AttributionOverlay has no Text target wired up; attribution would be invisible.");
                return;
            }

            attributionText.text = $"{attribution.Notice} — {attribution.OdblLicenseUrl}";
            gameObject.SetActive(true);
        }
    }
}
