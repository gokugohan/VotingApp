using System;
using System.Collections.Generic;
using System.Linq;
using VotingApp.Data.Elections;

namespace VotingApp.Services.Elections
{
    public sealed record ListPosition(int Position, Gender Gender);

    /// <summary>
    /// Checks a closed party list's gender quota: at least one woman in every block of three
    /// consecutive positions (1-3, 4-6, ...). Configure the block size per election if the law changes.
    /// </summary>
    public class PartyListValidator
    {
        public IReadOnlyList<ValidationIssue> Validate(IEnumerable<ListPosition> list, int blockSize = 3)
        {
            if (list == null) throw new ArgumentNullException(nameof(list));
            if (blockSize < 1) throw new ArgumentOutOfRangeException(nameof(blockSize));

            var ordered = list.OrderBy(p => p.Position).ToList();
            var issues = new List<ValidationIssue>();

            for (int i = 0; i < ordered.Count; i++)
            {
                if (ordered[i].Position != i + 1)
                {
                    issues.Add(new ValidationIssue("LIST_POSITIONS", IssueSeverity.Error,
                        "List positions must run 1, 2, 3, … without gaps or duplicates."));
                    return issues;
                }
            }

            // Only complete blocks are checked; a trailing partial block has no quota requirement.
            for (int start = 0; start + blockSize <= ordered.Count; start += blockSize)
            {
                var block = ordered.Skip(start).Take(blockSize).ToList();
                if (block.All(p => p.Gender != Gender.Female))
                {
                    issues.Add(new ValidationIssue("GENDER_QUOTA", IssueSeverity.Error,
                        $"Positions {block[0].Position}-{block[^1].Position} need at least one woman."));
                }
            }
            return issues;
        }
    }
}
