using System;
using System.Threading.Tasks;

namespace SpaceMaker
{
    /// <summary>自动更新信息。</summary>
    internal sealed class UpdateInfo
    {
        public bool HasUpdate { get; set; }
        public string Version { get; set; } = "";
        public string DownloadUrl { get; set; } = "";
        public string Notes { get; set; } = "";
    }

    /// <summary>更新源接口。当前仅占位，后续可接入 GitHub Releases。</summary>
    internal interface IUpdateSource
    {
        Task<UpdateInfo> CheckAsync();
    }

    /// <summary>默认实现：未配置更新源，仅返回"未配置"提示。</summary>
    internal sealed class DisabledUpdateSource : IUpdateSource
    {
        public Task<UpdateInfo> CheckAsync()
        {
            return Task.FromResult(new UpdateInfo
            {
                HasUpdate = false,
                Notes = "自动更新尚未配置。接口已预留，后续可接 GitHub Releases（详见 README）。"
            });
        }
    }

    /*
     * 将来要接入 GitHub 自更新时，新增一个实现即可，例如：
     *
     * internal sealed class GitHubUpdateSource : IUpdateSource
     * {
     *     private const string VersionUrl = "https://your.cdn/version.json";
     *     public async Task<UpdateInfo> CheckAsync()
     *     {
     *         using var http = new HttpClient();
     *         var json = await http.GetStringAsync(VersionUrl);
     *         // 解析 version / url / notes，与当前 AssemblyVersion 比较
     *         ...
     *     }
     * }
     *
     * 然后把 MainWindow 里的 _updater 换成 new GitHubUpdateSource()。
     */
}
