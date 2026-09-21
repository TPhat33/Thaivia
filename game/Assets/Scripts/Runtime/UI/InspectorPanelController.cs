// UNCOMPILED: never built by a Unity Editor. See
// game/Assets/Scripts/Runtime/README.md.
using System.Text;
using Thaivia.Core.Attributes;
using Thaivia.Core.MapPack;
using Thaivia.Core.Values;
using Thaivia.Runtime.SelectionHandling;
using UnityEngine;
using UnityEngine.UI;

namespace Thaivia.Runtime.UI
{
    /// <summary>
    /// Renders a selected feature's fields, with SOURCE / UNKNOWN /
    /// ASSUMPTION as three visually distinct rows -- never blended into
    /// one "value" column. Every row's text and color come from a single
    /// SourceValue{T}.Match(...) call, so there is exactly one place per
    /// field that decides how each of the three states looks, instead of
    /// three copies of similar-but-drifting formatting code.
    /// </summary>
    public sealed class InspectorPanelController : MonoBehaviour
    {
        [SerializeField] private Text? titleText;
        [SerializeField] private Text? bodyText;
        [SerializeField] private GameObject? panelRoot;

        // Colors are placeholders -- a human should replace these with the
        // project's real UI theme; the point being enforced here is that
        // three DIFFERENT colors are used, never one.
        private static readonly Color SourceColor = new(0.10f, 0.55f, 0.10f); // known: green
        private static readonly Color UnknownColor = new(0.55f, 0.55f, 0.55f); // unknown: grey
        private static readonly Color AssumedColor = new(0.85f, 0.55f, 0.05f); // assumed: amber

        public void Show(MapFeatureRef featureRef)
        {
            if (panelRoot != null)
            {
                panelRoot.SetActive(true);
            }

            if (featureRef.Polygon is { } polygon)
            {
                ShowPolygon(polygon);
            }
            else if (featureRef.Road is { } road)
            {
                ShowRoad(road);
            }
            else if (featureRef.Line is { } line)
            {
                if (titleText != null)
                {
                    titleText.text = $"{line.FeatureClass} #{line.SourceId}";
                }

                if (bodyText != null)
                {
                    bodyText.text = FormatSourceTags(line.SourceTags);
                }
            }
        }

        public void Hide()
        {
            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }
        }

        private void ShowPolygon(PolygonFeature feature)
        {
            if (titleText != null)
            {
                titleText.text = $"{feature.FeatureClass} #{feature.SourceId}";
            }

            var sb = new StringBuilder();
            AppendRow(sb, "Height", BuildingAttributes.GetHeightMeters(feature), v => $"{v:0.#} m");
            AppendRow(sb, "Use", BuildingAttributes.GetBuildingUse(feature), v => v);
            sb.AppendLine();
            sb.Append(FormatSourceTags(feature.SourceTags));

            if (bodyText != null)
            {
                bodyText.text = sb.ToString();
            }
        }

        private void ShowRoad(RoadEdge road)
        {
            if (titleText != null)
            {
                titleText.text = $"road way #{road.WayId}";
            }

            var sb = new StringBuilder();
            AppendRow(sb, "Width", RoadAttributes.GetWidthMeters(road), v => $"{v:0.#} m");
            AppendRow(sb, "Lanes", RoadAttributes.GetLaneCount(road), v => v.ToString());
            AppendRow(sb, "Max speed", RoadAttributes.GetMaxSpeedKph(road), v => $"{v} km/h");
            sb.AppendLine();
            sb.Append(FormatSourceTags(road.SourceTags));

            if (bodyText != null)
            {
                bodyText.text = sb.ToString();
            }
        }

        private static void AppendRow<T>(StringBuilder sb, string label, SourceValue<T> value, System.Func<T, string> format)
        {
            var line = value.Match(
                onKnown: v => $"<color=#{ColorHex(SourceColor)}>{label}: {format(v)} (source)</color>",
                onUnknown: () => $"<color=#{ColorHex(UnknownColor)}>{label}: unknown</color>",
                onAssumed: (v, rule, kind) =>
                    $"<color=#{ColorHex(AssumedColor)}>{label}: {format(v)} (assumed -- {kind}, rule: {rule})</color>");
            sb.AppendLine(line);
        }

        private static string FormatSourceTags(SourceTags tags)
        {
            if (tags.Tags.Count == 0)
            {
                return "(no OSM tags on this feature)";
            }

            var sb = new StringBuilder("Source tags:\n");
            foreach (var kv in tags.Tags)
            {
                sb.AppendLine($"  {kv.Key} = {kv.Value}");
            }

            return sb.ToString();
        }

        private static string ColorHex(Color c) => ColorUtility.ToHtmlStringRGB(c);
    }
}
