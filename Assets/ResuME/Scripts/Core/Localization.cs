using System;
using UnityEngine;

namespace GGS.ResuME.Data
{
    /// <summary>
    /// The language of the session and the text lookups every display goes through. The language
    /// is chosen once at start-up (language screen, pre-selected from the host name, a query
    /// parameter or the browser language) and never stored: the game keeps no trace of its visitors.
    /// </summary>
    public static class Localization
    {
        #region Fields

        private const string ENGLISH_HOST_PREFIX = "resume.";
        private const string FRENCH_HOST_PREFIX = "cv.";
        private const string QUERY_PARAMETER = "lang";

        /// <summary>Language of the session, French until chosen.</summary>
        public static Language Current { get; set; } = Language.Fr;

        #endregion

        #region Public Methods

        /// <summary>
        /// Text of a bilingual field in the session language.
        /// </summary>
        /// <param name="text">Field of the route; null gives an empty string.</param>
        /// <returns>The text, French when the translation is missing.</returns>
        public static string Text(LocalizedText text)
        {
            return text != null ? text.Get(Current) : "";
        }

        /// <summary>
        /// Word of the interface by id, in the session language.
        /// </summary>
        /// <param name="route">Route holding the strings table.</param>
        /// <param name="id">String id as written in route.json.</param>
        /// <returns>The word, French when the translation is missing, the id itself when unknown.</returns>
        public static string Ui(RouteDefinition route, string id)
        {
            UiStringDefinition entry = route != null ? route.FindString(id) : null;
            if (entry == null) return id;
            if (Current == Language.En && !string.IsNullOrEmpty(entry.en)) return entry.en;
            return entry.fr ?? id;
        }

        /// <summary>
        /// Name of a skill category in the session language.
        /// </summary>
        /// <param name="route">Route holding the strings table.</param>
        /// <param name="category">Category to name.</param>
        /// <returns>The name, or an empty string for None.</returns>
        public static string Category(RouteDefinition route, SkillCategory category)
        {
            return category == SkillCategory.None ? "" : Ui(route, "category-" + category.ToString().ToLowerInvariant());
        }

        /// <summary>
        /// Language to pre-select at start-up: the query parameter first (lang=fr or lang=en), then the
        /// host name (resume. is English, cv. is French), then the browser language.
        /// </summary>
        /// <param name="url">Page URL (Application.absoluteURL), may be empty outside WebGL.</param>
        /// <param name="systemLanguage">Browser or system language.</param>
        /// <returns>The language to pre-select.</returns>
        public static Language Detect(string url, SystemLanguage systemLanguage)
        {
            if (!string.IsNullOrEmpty(url))
            {
                // Query parameter: the only way to force a language on a host that names none (localhost).
                // Only the real "lang" parameter counts, not a "lang=" inside another name (bug B17).
                string code = QueryValue(url, QUERY_PARAMETER);
                if (code == "en") return Language.En;
                if (code == "fr") return Language.Fr;

                // Host name: cv.gorasgames.fr is French, resume.gorasgames.fr is English.
                if (Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
                {
                    string host = uri.Host.ToLowerInvariant();
                    if (host.StartsWith(ENGLISH_HOST_PREFIX)) return Language.En;
                    if (host.StartsWith(FRENCH_HOST_PREFIX)) return Language.Fr;
                }
            }
            return systemLanguage == SystemLanguage.French ? Language.Fr : Language.En;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Value of a query parameter of a URL, lower case, or null when absent.
        /// </summary>
        /// <param name="url">Full URL.</param>
        /// <param name="name">Parameter name, matched exactly (case-insensitive).</param>
        /// <returns>The value, or null.</returns>
        private static string QueryValue(string url, string name)
        {
            int start = url.IndexOf('?');
            if (start < 0) return null;
            int end = url.IndexOf('#', start);
            string query = end < 0 ? url.Substring(start + 1) : url.Substring(start + 1, end - start - 1);
            foreach (string pair in query.Split('&'))
            {
                int equals = pair.IndexOf('=');
                string key = equals < 0 ? pair : pair.Substring(0, equals);
                if (!string.Equals(Uri.UnescapeDataString(key), name, StringComparison.OrdinalIgnoreCase)) continue;
                return equals < 0 ? "" : Uri.UnescapeDataString(pair.Substring(equals + 1)).ToLowerInvariant();
            }
            return null;
        }

        #endregion
    }
}
