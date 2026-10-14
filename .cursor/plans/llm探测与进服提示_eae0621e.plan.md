---
name: LLM探测与进服提示
overview: 进世界后用配置真实探测一次 LLM；联机提示挂在 ChatHistoryRequest（加入世界）；并清掉仅凭 Key、无效 OnEnterWorld 提示等无用代码。
todos:
  - id: probe-api
    content: AiChatClient.ProbeAsync + AiRuntimeGate 探测状态机（替换仅 HasCredentials 的 Refresh 门控）
    status: completed
  - id: world-start-probe
    content: OnWorldLoad/OnChanged 启探测；失败写 AutoDisabledNoModel 并补发待提示队列
    status: completed
  - id: join-tip
    content: ChatHistoryRequest 末尾触发联机提示；单机探测完成后提示
    status: completed
  - id: cleanup
    content: 删除无效 OnEnterWorld 联机提示路径、旧 Refresh/轮询残留、无引用辅助方法
    status: completed
isProject: false
---

# LLM 探测判定 + 加入世界提示

## 判定怎么改

现状：[`AiRuntimeGate.Refresh`](Common/Ai/Chat/AiRuntimeGate.cs) 只看 `HasCredentials`，不打 API。

改为：

1. 世界加载（单机权威 / 专用服）且 `AiEnabled || SuperpowerEnabled` → **探测一次**。
2. 无 Key/Model → 不发 HTTP，直接失败。
3. 有凭据 → 极短 `chat/completions`（`ping`，`max_tokens=1`）。
4. 成功 → `AutoDisabledNoModel = false`；失败 → `true`。
5. 配置 `OnChanged` → 重探测；失败则提示在线玩家。

[`AiChatClient.ProbeAsync`](Common/Ai/Chat/AiChatClient.cs)：只要求 `HasCredentials`，不走 `IsConfigured`（避免只开超能力时探不到）。

`AiRuntimeGate`：Idle / Running / Ok / Fail；`IsReady` / 超能力门控仍读 `AutoDisabledNoModel`。

## 提示何时发

联机：客户端进世界已发 [`ChatHistoryRequest`](Common/Net/NpcChatMessageType.cs)；服务端 [`HandleHistoryRequest`](Common/ChatLog/ChatLogWorldSystem.cs) 末尾 `TryNotifyNoModel(whoAmI)`（探测中则入队，结束后补发）。

单机：探测完成且失败 → 提示 `Main.myPlayer`。

## 清理无用代码（本轮一并做）

- 删除或重写 [`NpcAiNoModelNoticePlayer.OnEnterWorld`](Common/Ai/Chat/NpcAiNoModelNoticePlayer.cs) 里「服务端发提示 / 客户端直接 return」的无效联机路径；能并入 `NpcAiChatSystem` 则不留空壳文件。
- 去掉仅凭 Key 的 `Refresh` 作为可用性判定（改为探测结果驱动）；`HasCredentials` 只作探测前置。
- 扫掉无引用：`ResetNoModelNotices` / `NotifyAllOnlineNoModel` 若被探测完成回调取代则删除或合并为一个入口。
- 确认无残留 `PostUpdatePlayers` 无模型轮询。
- `AiRuntimeGate.Clear` / `OnWorldUnload` 与探测取消对齐，不留半套状态。

## 主要改动文件

- [`AiChatClient.cs`](Common/Ai/Chat/AiChatClient.cs)、[`AiRuntimeGate.cs`](Common/Ai/Chat/AiRuntimeGate.cs)
- [`NpcAiChatSystem.cs`](Common/Ai/Chat/NpcAiChatSystem.cs)、[`NpcAiNoModelNoticePlayer.cs`](Common/Ai/Chat/NpcAiNoModelNoticePlayer.cs)
- [`ChatLogWorldSystem.cs`](Common/ChatLog/ChatLogWorldSystem.cs)
- [`AinpcServerConfig.cs`](Common/Configs/AinpcServerConfig.cs)

## 验收

1. 无 Key / 错 Key → 进世界后失败提示，AI/超能力关。
2. 正确 Key → 探测成功，无提示。
3. 专用服进服（拉历史）后能收到提示；无死代码路径再依赖服务端 `OnEnterWorld`。
