// Pure C# -- no UnityEngine reference. See game/README.md.
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Thaivia.Core.Serialization;

/// <summary>
/// Recomputes the exact byte sequence
/// `map_pipeline.pipeline.mappack.canonical_json_bytes` produces in
/// Python (`json.dumps(payload, sort_keys=True, separators=(",", ":"),
/// ensure_ascii=True)`), from an already-parsed <see cref="JsonElement"/>,
/// so `content_hash` can be independently re-derived and compared rather
/// than trusted.
///
/// The trick that makes this byte-identical without reimplementing
/// Python's float/string formatting: `thaivia build` writes the whole
/// MapPack file with `json.dumps(pack, indent=2, sort_keys=True)`, whose
/// default `ensure_ascii=True` matches the canonical form's escaping, and
/// whose number/string *tokens* are the very same tokens the canonical
/// serializer would emit (indentation only adds/removes whitespace
/// between tokens, never changes a token's own text). So instead of
/// re-formatting each leaf value, this walker copies each leaf's exact
/// original source substring via <see cref="JsonElement.GetRawText"/> and
/// only re-orders/re-joins at the object/array level. See
/// docs/decisions for the ADR on this approach and
/// Thaivia.Core.Tests.CanonicalJsonTests for the property that a
/// re-derived hash matches a real pipeline-produced pack, and diverges
/// after a one-byte tamper.
/// </summary>
public static class CanonicalJson
{
    public static byte[] CanonicalBytes(JsonElement element)
    {
        var sb = new StringBuilder();
        Write(element, sb);
        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    public static string ComputeContentHash(JsonElement payload)
    {
        var bytes = CanonicalBytes(payload);
        var hash = SHA256.HashData(bytes);
        return "sha256:" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static void Write(JsonElement element, StringBuilder sb)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                sb.Append('{');
                var props = element.EnumerateObject()
                    .OrderBy(p => p.Name, StringComparer.Ordinal)
                    .ToList();
                for (var i = 0; i < props.Count; i++)
                {
                    if (i > 0)
                    {
                        sb.Append(',');
                    }

                    // Property names go through the same escaping Python's
                    // encoder uses for keys; re-encode via a JsonElement of
                    // kind String is not directly available for a raw
                    // property name, so we encode it exactly like a JSON
                    // string value using System.Text.Json, then verify in
                    // tests that this matches real pipeline output for the
                    // ASCII key set the MapPack schema actually uses.
                    WriteEscapedKey(props[i].Name, sb);
                    sb.Append(':');
                    Write(props[i].Value, sb);
                }

                sb.Append('}');
                break;

            case JsonValueKind.Array:
                sb.Append('[');
                var first = true;
                foreach (var item in element.EnumerateArray())
                {
                    if (!first)
                    {
                        sb.Append(',');
                    }

                    first = false;
                    Write(item, sb);
                }

                sb.Append(']');
                break;

            case JsonValueKind.String:
            case JsonValueKind.Number:
            case JsonValueKind.True:
            case JsonValueKind.False:
            case JsonValueKind.Null:
                // Leaf tokens are copied verbatim from the source document
                // (see class doc) -- this is what makes the hash byte-exact
                // against Python's own canonical serialization without a
                // from-scratch float/escape reimplementation.
                sb.Append(element.GetRawText());
                break;

            default:
                throw new InvalidOperationException($"Unexpected JSON value kind: {element.ValueKind}");
        }
    }

    /// <summary>
    /// Escapes a JSON object key exactly the way Python's json encoder does
    /// under `ensure_ascii=True`: backslash/doublequote escaped, the
    /// standard single-letter control escapes for \b \f \n \r \t, every
    /// other char outside printable ASCII (0x20-0x7E) as \uXXXX (with a
    /// UTF-16 surrogate pair written as two \uXXXX units for chars beyond
    /// the BMP, matching CPython's `encode_basestring_ascii`). Leaf string
    /// *values* never go through here -- they are copied verbatim via
    /// GetRawText (see <see cref="Write"/>) -- this path only matters for
    /// object keys, which System.Text.Json always hands back unescaped.
    /// </summary>
    private static void WriteEscapedKey(string key, StringBuilder sb)
    {
        sb.Append('"');
        foreach (var ch in key)
        {
            switch (ch)
            {
                case '\\':
                    sb.Append("\\\\");
                    break;
                case '"':
                    sb.Append("\\\"");
                    break;
                case '\b':
                    sb.Append("\\b");
                    break;
                case '\f':
                    sb.Append("\\f");
                    break;
                case '\n':
                    sb.Append("\\n");
                    break;
                case '\r':
                    sb.Append("\\r");
                    break;
                case '\t':
                    sb.Append("\\t");
                    break;
                default:
                    if (ch is >= (char)0x20 and <= (char)0x7E)
                    {
                        sb.Append(ch);
                    }
                    else
                    {
                        // `ch` is already one UTF-16 code unit (a surrogate
                        // half for astral characters), which is exactly
                        // what Python's ensure_ascii=True emits per unit.
                        sb.Append("\\u").Append(((int)ch).ToString("x4"));
                    }

                    break;
            }
        }

        sb.Append('"');
    }
}
