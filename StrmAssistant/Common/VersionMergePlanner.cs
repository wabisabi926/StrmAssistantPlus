using System;
using System.Collections.Generic;
using System.Linq;

namespace StrmAssistant.Common
{
    internal sealed class VersionMergePlan<T>
    {
        public List<List<T>> Groups { get; } = new List<List<T>>();
        public List<VersionMergeConflict<T>> Conflicts { get; } = new List<VersionMergeConflict<T>>();
    }

    internal sealed class VersionMergeConflict<T>
    {
        public List<T> Items { get; } = new List<T>();
        public string ProviderKey { get; set; }
        public List<string> Values { get; } = new List<string>();
    }

    internal static class VersionMergePlanner
    {
        public static VersionMergePlan<T> Plan<T>(IEnumerable<T> items, Func<T, long> itemId,
            Func<T, IEnumerable<KeyValuePair<string, string>>> providerIds)
        {
            var plan = new VersionMergePlan<T>();
            var itemOrder = new List<long>();
            var itemLookup = new Dictionary<long, T>();
            var providers = new Dictionary<long, List<KeyValuePair<string, string>>>();

            foreach (var item in items)
            {
                if (item is null) continue;
                var id = itemId(item);
                if (itemLookup.ContainsKey(id)) continue;

                itemOrder.Add(id);
                itemLookup[id] = item;
                var normalized = new List<KeyValuePair<string, string>>();
                foreach (var pair in providerIds(item) ?? Enumerable.Empty<KeyValuePair<string, string>>())
                {
                    if (!ProviderKeys.Contains(pair.Key) || string.IsNullOrWhiteSpace(pair.Value)) continue;
                    normalized.Add(new KeyValuePair<string, string>(pair.Key.Trim().ToLowerInvariant(),
                        pair.Value.Trim().ToLowerInvariant()));
                }

                providers[id] = normalized;
            }

            var parent = itemOrder.ToDictionary(id => id, id => id);
            var providerGroups = itemOrder
                .SelectMany(id => providers[id].Select(pair => new { Id = id, pair.Key, pair.Value }))
                .GroupBy(x => new { x.Key, x.Value })
                .Where(g => g.Select(x => x.Id).Distinct().Count() > 1)
                .ToList();

            foreach (var group in providerGroups)
            {
                var ids = group.Select(x => x.Id).Distinct().ToArray();
                var root = ids[0];
                foreach (var id in ids.Skip(1))
                {
                    Union(root, id, parent);
                }
            }

            var components = itemOrder.GroupBy(id => Find(id, parent))
                .OrderBy(g => g.Min())
                .ToList();

            foreach (var component in components)
            {
                var memberIds = component.OrderBy(id => id).ToList();
                if (memberIds.Count < 2) continue;

                var conflict = FindConflict<T>(memberIds, providers);
                if (conflict != null)
                {
                    conflict.Items.AddRange(memberIds.Select(id => itemLookup[id]));
                    plan.Conflicts.Add(conflict);
                    continue;
                }

                plan.Groups.Add(memberIds.Select(id => itemLookup[id]).ToList());
            }

            return plan;
        }

        private static VersionMergeConflict<T> FindConflict<T>(List<long> memberIds,
            Dictionary<long, List<KeyValuePair<string, string>>> providers)
        {
            var valuesByProvider = memberIds
                .SelectMany(id => providers[id])
                .GroupBy(pair => pair.Key)
                .OrderBy(group => group.Key)
                .ToList();

            foreach (var provider in valuesByProvider)
            {
                var values = provider.Select(pair => pair.Value).Distinct().OrderBy(value => value).ToList();
                if (values.Count <= 1) continue;

                var conflict = new VersionMergeConflict<T>
                {
                    ProviderKey = provider.Key
                };
                conflict.Values.AddRange(values);
                return conflict;
            }

            return null;
        }

        private static long Find(long id, Dictionary<long, long> parent)
        {
            var root = id;
            while (parent[root] != root)
            {
                root = parent[root];
            }

            while (parent[id] != id)
            {
                var next = parent[id];
                parent[id] = root;
                id = next;
            }

            return root;
        }

        private static void Union(long left, long right, Dictionary<long, long> parent)
        {
            var leftRoot = Find(left, parent);
            var rightRoot = Find(right, parent);
            if (leftRoot != rightRoot)
            {
                parent[rightRoot] = leftRoot;
            }
        }

        private static readonly HashSet<string> ProviderKeys =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "tmdb", "imdb", "tvdb" };
    }
}
