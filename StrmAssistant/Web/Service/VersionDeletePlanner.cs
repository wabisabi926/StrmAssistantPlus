using MediaBrowser.Controller.Entities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace StrmAssistant.Web.Service
{
    internal sealed class VersionDeletePlan
    {
        public VersionDeletePlan(IReadOnlyList<BaseItem> authorized, int unauthorizedCount, bool isDenied)
        {
            Authorized = authorized;
            UnauthorizedCount = unauthorizedCount;
            IsDenied = isDenied;
        }

        public IReadOnlyList<BaseItem> Authorized { get; }
        public int UnauthorizedCount { get; }
        public bool IsDenied { get; }
    }

    internal static class VersionDeletePlanner
    {
        public static VersionDeletePlan Build(BaseItem root, IEnumerable<BaseItem> candidates,
            Func<BaseItem, bool> canDelete)
        {
            if (root == null || canDelete == null || !canDelete(root))
            {
                return new VersionDeletePlan(Array.Empty<BaseItem>(), 0, true);
            }

            var authorized = new List<BaseItem> { root };
            var unauthorizedCount = 0;
            var seen = new HashSet<BaseItem> { root };

            foreach (var candidate in candidates ?? Enumerable.Empty<BaseItem>())
            {
                if (candidate == null || !seen.Add(candidate)) continue;

                if (canDelete(candidate))
                {
                    authorized.Add(candidate);
                }
                else
                {
                    unauthorizedCount++;
                }
            }

            return new VersionDeletePlan(authorized, unauthorizedCount, false);
        }
    }
}
