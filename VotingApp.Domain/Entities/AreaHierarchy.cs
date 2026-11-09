using System;

namespace VotingApp.Domain.Entities
{
    /// <summary>
    /// Administrative levels of <see cref="Area"/> and helpers for the hierarchical area codes
    /// (e.g. municipality "TLS001", administrative post "TLS00101", suco "TLS0010101").
    /// </summary>
    public static class AreaHierarchy
    {
        public const int Municipality = 2;
        public const int AdministrativePost = 3;
        public const int Village = 4;

        /// <summary>Length of a municipality code; every descendant's code starts with it.</summary>
        public const int MunicipalityCodeLength = 6;

        /// <summary>The municipality code an area belongs to, derived from its hierarchical code.</summary>
        public static string MunicipalityIdOf(string areaId)
        {
            if (string.IsNullOrEmpty(areaId) || areaId.Length < MunicipalityCodeLength)
            {
                throw new ArgumentException($"'{areaId}' is not a valid area code.", nameof(areaId));
            }
            return areaId.Substring(0, MunicipalityCodeLength);
        }
    }
}
