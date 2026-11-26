using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Xml;

namespace PowerOptionEditor
{
    static class Ui
    {
        const string ResourcePrefix = "PowerOptionEditor.Locales.";
        static readonly Dictionary<string, string> English = Load("en");
        static readonly Dictionary<string, string> Current = Detect();

        public static string Title { get { return T("power.option.editor"); } }

        public static string T(string key)
        {
            string value;
            if (Current.TryGetValue(key, out value)) return value;
            return English.TryGetValue(key, out value) ? value : key;
        }

        static Dictionary<string, string> Detect()
        {
            for (CultureInfo culture = CultureInfo.CurrentUICulture;
                culture != null && culture.Name.Length > 0; culture = culture.Parent)
            {
                Dictionary<string, string> strings = Load(culture.Name);
                if (strings.Count > 0) return strings;
                string name = culture.Name;
                if (name.Equals("zh-CN", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("zh-SG", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("zh-CHS", StringComparison.OrdinalIgnoreCase))
                    return Load("zh-Hans");
                if (name.Equals("zh-TW", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("zh-HK", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("zh-MO", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("zh-CHT", StringComparison.OrdinalIgnoreCase))
                    return Load("zh-Hant");
            }
            return English;
        }

        static Dictionary<string, string> Load(string culture)
        {
            Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.Ordinal);
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourcePrefix + culture + ".xml"))
            {
                if (stream == null) return result;
                XmlReaderSettings settings = new XmlReaderSettings();
                settings.DtdProcessing = DtdProcessing.Prohibit;
                settings.XmlResolver = null;
                using (XmlReader reader = XmlReader.Create(stream, settings))
                {
                    XmlDocument document = new XmlDocument();
                    document.PreserveWhitespace = true;
                    document.XmlResolver = null;
                    document.Load(reader);
                    foreach (XmlNode node in document.SelectNodes("/language/string"))
                    {
                        string key = node.Attributes["key"].Value;
                        if (!string.IsNullOrEmpty(node.InnerText)) result.Add(key, node.InnerText);
                    }
                }
            }
            return result;
        }
    }
}
