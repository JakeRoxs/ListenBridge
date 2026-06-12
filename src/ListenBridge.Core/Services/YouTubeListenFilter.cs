using System.Collections.Generic;
using System.Linq;
using ListenBridge.Core.Domain;

namespace ListenBridge.Core.Services;

public static class YouTubeListenFilter
{
    public static IReadOnlyList<Listen> Apply(IEnumerable<Listen> listens, YouTubeListenFilterOptions? options)
    {
        if (listens is null)
        {
            throw new ArgumentNullException(nameof(listens));
        }

        var filtered = options is null || options.IncludeTopicChannels
            ? listens
            : listens.Where(listen => !listen.IsTopicChannel);

        if (options is null)
        {
            return filtered as IReadOnlyList<Listen> ?? filtered.ToList();
        }

        if (options.SkipThresholdSeconds.HasValue && options.SkipThresholdSeconds.Value > 0)
        {
            filtered = ApplyDeduplication(filtered, options.SkipThresholdSeconds.Value);
        }

        if (options.DeduplicationWindowSeconds.HasValue && options.DeduplicationWindowSeconds.Value > 0)
        {
            filtered = ApplyDeduplication(filtered, options.DeduplicationWindowSeconds.Value);
        }

        return filtered as IReadOnlyList<Listen> ?? filtered.ToList();
    }

    private static IReadOnlyList<Listen> ApplyDeduplication(IEnumerable<Listen> listens, int windowSeconds)
    {
        var ordered = listens.OrderBy(listen => listen.ListenedAt).ToList();
        if (ordered.Count <= 1)
        {
            return ordered;
        }

        var deduplicated = new List<Listen>();
        var cluster = new List<Listen> { ordered[0] };

        for (var index = 1; index < ordered.Count; index++)
        {
            var current = ordered[index];
            var lastInCluster = cluster[^1];

            if (lastInCluster.GetIdentity().Equals(current.GetIdentity()) && (current.ListenedAt - lastInCluster.ListenedAt).TotalSeconds <= windowSeconds)
            {
                cluster.Add(current);
                continue;
            }

            deduplicated.Add(cluster[^1]);
            cluster = new List<Listen> { current };
        }

        deduplicated.Add(cluster[^1]);
        return deduplicated;
    }
}
