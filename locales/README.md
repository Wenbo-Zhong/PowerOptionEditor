# Translating Power Option Editor

All application-owned interface text lives here. Setting names, descriptions,
units and choices supplied by Windows retain the language provided by Windows.

## Add a language

1. Copy `en.xml` to a file named after a .NET Framework 4 culture, such as
   `de.xml`, `fr.xml`, `ja.xml`, or `pt-BR.xml`.
2. Set the root `culture` attribute to the same culture name as the filename.
3. Translate the text inside each `<string>` element. Keep every `key` unchanged:

   ```xml
   <language culture="de" xml:space="preserve">
     <string key="daily">Alltag</string>
     <string key="scenarios">Szenarien</string>
   </language>
   ```

4. Preserve leading/trailing spaces and line breaks: some entries are fragments
   joined to a setting name, number or error message. Use XML escapes such as
   `&amp;` and `&lt;` where necessary. Save as UTF-8.
5. Run `python scripts/validate-locales.py` from the repository root (Python 3,
   no third-party packages), then build using the instructions in the main README.
6. Submit the new XML file in a pull request. No C# or project-file change is
   needed to register a language.

Files are automatically embedded at build time, so distributing the EXE alone
still works. Editing an XML file requires rebuilding the application.

## Language selection and fallback

The application follows the Windows UI culture. It tries the exact culture
(for example `fr-CA`), then parent cultures (`fr`), then English. The existing
Chinese region mappings remain supported: CN/SG use `zh-Hans`, TW/HK/MO use
`zh-Hant`. An exact regional translation takes precedence over these mappings.
Missing or empty entries in the selected language fall back to `en.xml`, so
partial translations are welcome. Do not translate or rename keys.

`en.xml` is the reference catalog. `zh-Hans.xml` and `zh-Hant.xml` contain the
existing Simplified and Traditional Chinese translations. For new interface
text, add a stable key to `en.xml` and reference it with `Ui.T("key")`; avoid
changing existing keys when rewording text.
