using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace ShipCoreFramework
{
    public partial class ModConfig
    {
        private readonly List<string> _contentFingerprintInputs = new List<string>();

        [XmlIgnore]
        internal string ContentFingerprint { get; private set; } = string.Empty;

        private void TrackContentFile(string fileName, string text)
        {
            var normalizedText = (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');
            _contentFingerprintInputs.Add((fileName ?? string.Empty) + "\n" + normalizedText);
        }

        private void FinalizeContentFingerprint()
        {
            _contentFingerprintInputs.Sort(StringComparer.Ordinal);

            const ulong offset = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            var hash = offset;
            unchecked
            {
                foreach (var input in _contentFingerprintInputs)
                {
                    foreach (var character in input)
                    {
                        hash ^= (byte)character;
                        hash *= prime;
                        hash ^= (byte)(character >> 8);
                        hash *= prime;
                    }
                    hash ^= 0xff;
                    hash *= prime;
                }
            }

            ContentFingerprint = hash.ToString("X16");
            _contentFingerprintInputs.Clear();
        }
    }
}
