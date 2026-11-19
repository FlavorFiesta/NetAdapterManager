# 免费备用更新源

已部署地址：https://netadaptermanager-updates.482822012.workers.dev/latest.json

客户端先检查 GitHub，连接、证书、DNS、代理、超时、限流或服务器错误时切换备用源。每个源最多等待 10 秒，支持取消。设置中可修改地址或清空禁用；旧设置缺少该字段时使用默认地址，显式空值仍禁用。

## Cloudflare Worker

update-worker/worker.mjs 从官方 GitHub API 获取最新正式版，校验版本和发布链接后缓存 5 分钟。API 限流时使用 GitHub 官方 releases/latest 页面的重定向确认最新正式版，此时更新摘要为空。发布 GitHub Release 后无需手动上传 JSON。查询失败返回 503，不将旧的随包快照当成最新版本；不接受任意转发地址，不转发客户端凭据。

使用 Cloudflare Workers 免费方案，无需自备服务器或购买域名。额度以官方说明为准：https://developers.cloudflare.com/workers/platform/limits/

项目根目录的部署及验证命令（登录有权限的 Cloudflare 账户）：

```powershell
npx wrangler@4.156.1 login
npx wrangler@4.156.1 deploy --config update-worker/wrangler.jsonc --no-bundle
node --test update-worker/worker.test.mjs
dotnet run --project tests/NetAdapterManager.SelfTest -c Release -- --update-fallback-live
```

--no-bundle 直接上传独立模块，避免本机临时打包目录访问失败。不要提交 OAuth 文件或令牌。部署工具需要代理时，可设置当前终端的 HTTP_PROXY/HTTPS_PROXY。软件使用 Windows/.NET 默认代理设置，不硬编码代理端口。

本机测试中 workers.dev 直连超时，需要现有代理；不保证在所有网络下直连可用。备用源仅提供版本信息，安装包仍从本项目 GitHub Release 下载，没有自动安装行为，也没有关闭 TLS 校验。

build/Build-UpdateFeed.ps1 仅保留为可选静态快照导出工具，不参与线上更新。旧的 Pages 工作流已移除。

## 本机证书修复

2026-11-02 仅向当前用户 Root 存储补入 USERTrust ECC Certification Authority。SHA-256 已与微软 Included CA Certificate List 核对：4FF460D54B9C86DABFBCFC5712E0400D2BED3FBC4D4FBDAA86E06ADCD2A9AD7A。原始文件、修复记录和撤回命令在 %LOCALAPPDATA%/NetAdapterManager/certificate-repair-20261102。程序本身不会修改证书库。
