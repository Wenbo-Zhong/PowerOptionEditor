using System;
using System.Collections.Generic;

namespace PowerOptionEditor
{
    static class ProfileDefinitions
    {
        // Known processor scenarios. The local Profile\Events registry overrides priorities.
        static readonly ProfileDefinition[] Definitions = {
            new ProfileDefinition("0da965dc-8fcf-4c0b-8efe-8dd5e7bc959a", "low.latency",             40),
            new ProfileDefinition("0aabb002-a307-447e-9b81-1d819df6c6d0", "sustained.performance",   35),
            new ProfileDefinition("d4140c81-ebba-4e60-8561-6918290359cd", "game.mode",               32),
            new ProfileDefinition("4569e601-272e-4869-bcab-1c6c03d7966f", "low.power",               30),
            new ProfileDefinition("8bc6262c-c026-411d-ae3b-7e2f70811a13", "standby",                 20),
            new ProfileDefinition("2e92e666-c3f6-42c3-89bd-94d40fabcde5", "screen.off",              19),
            new ProfileDefinition("ee1e4f72-e368-46b1-b3c6-5048b11c2dbd", "constrained",             10),
            new ProfileDefinition("b025fe59-79f4-4e38-b142-bcc62c7ecc32", "high.drain",               5),
            new ProfileDefinition("33cc3a0d-45ee-43ca-86c4-695bfc9a313b", "utility.qos",              4),
            new ProfileDefinition("336c7511-f109-4172-bb3a-3ea51f815ada", "eco.qos",                  3),
            new ProfileDefinition("0c3d5326-944b-4aab-8ad8-fe422a0e50e0", "multimedia.qos",           2),
            new ProfileDefinition("a4a61b5f-f42c-4d23-b3ab-5c27df9f0f18", "entry.level.performance",  1),
            new ProfileDefinition("c04a802d-2205-4910-ae98-3b51e3bb72f2", "background",               0)
        };

        public static List<ProfileItem> CreateProfiles()
        {
            List<ProfileItem> profiles = new List<ProfileItem>();
            foreach (ProfileDefinition definition in Definitions)
                profiles.Add(new ProfileItem {
                    Guid = definition.Guid,
                    Name = Ui.T(definition.NameKey),
                    Priority = definition.Priority
                });
            return profiles;
        }

        sealed class ProfileDefinition
        {
            public readonly Guid Guid;
            public readonly string NameKey;
            public readonly int Priority;

            public ProfileDefinition(string guid, string nameKey, int priority)
            {
                Guid = new Guid(guid);
                NameKey = nameKey;
                Priority = priority;
            }
        }
    }
}
