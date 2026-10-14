using System;
using System.Threading;
using System.Threading.Tasks;
using AITownNPCs.Common.Configs;
using Terraria.ModLoader;

namespace AITownNPCs.Common.Ai.Chat
{
	public enum AiProbePhase : byte
	{
		Idle = 0,
		Running = 1,
		Ok = 2,
		Fail = 3
	}

	/// <summary>
	/// Session gate from a real LLM probe (not credential presence alone).
	/// Does not mutate saved ModConfig toggles.
	/// </summary>
	public static class AiRuntimeGate
	{
		private static CancellationTokenSource _probeCts;
		private static int _probeGeneration;

		public static AiProbePhase Phase { get; private set; } = AiProbePhase.Idle;

		/// <summary>True after a failed probe (show tip / hard-disable AI features).</summary>
		public static bool AutoDisabledNoModel => Phase == AiProbePhase.Fail;

		/// <summary>True only when probe succeeded; AI/superpowers may run.</summary>
		public static bool CanUseAiFeatures => Phase == AiProbePhase.Ok;

		/// <summary>Start or restart probe. <paramref name="onFinished"/> runs on a thread-pool thread.</summary>
		public static void StartProbe(AinpcServerConfig config, Action onFinished = null) {
			CancelProbe();

			if (config == null || (!config.AiEnabled && !config.SuperpowerEnabled)) {
				Phase = AiProbePhase.Idle;
				onFinished?.Invoke();
				return;
			}

			if (!AiChatClient.HasCredentials(config)) {
				Phase = AiProbePhase.Fail;
				ModContent.GetInstance<AITownNPCs>()?.Logger.Info(
					"AI probe failed: missing API key or model");
				onFinished?.Invoke();
				return;
			}

			Phase = AiProbePhase.Running;
			int gen = ++_probeGeneration;
			_probeCts = new CancellationTokenSource();
			CancellationToken ct = _probeCts.Token;

			Task.Run(async () => {
				bool ok = false;
				try {
					ok = await AiChatClient.ProbeAsync(config, ct).ConfigureAwait(false);
				}
				catch (OperationCanceledException) {
					return;
				}
				catch (Exception ex) {
					ModContent.GetInstance<AITownNPCs>()?.Logger.Warn(
						"AI probe exception: " + ex.GetBaseException().Message);
					ok = false;
				}

				if (gen != _probeGeneration || ct.IsCancellationRequested) {
					return;
				}

				Phase = ok ? AiProbePhase.Ok : AiProbePhase.Fail;
				ModContent.GetInstance<AITownNPCs>()?.Logger.Info(
					ok ? "AI probe ok" : "AI probe failed");
				onFinished?.Invoke();
			}, ct);
		}

		public static void Clear() {
			CancelProbe();
			Phase = AiProbePhase.Idle;
		}

		private static void CancelProbe() {
			_probeGeneration++;
			try {
				_probeCts?.Cancel();
				_probeCts?.Dispose();
			}
			catch {
				// ignore
			}

			_probeCts = null;
			if (Phase == AiProbePhase.Running) {
				Phase = AiProbePhase.Idle;
			}
		}
	}
}
