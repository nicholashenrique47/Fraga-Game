using UnityEngine;
using System;

public class MonetizationManager : MonoBehaviour
{
    [Header("Ad Settings")]
    public string gameId = "1234567"; // Mock ID
    public bool testMode = true;
    public string rewardedVideoAdUnitId = "Rewarded_Android";

    [Header("Dependencies")]
    public HubManager hubManager;
    public SanitySystem playerSanity;

    private Action onRewardCallback;

    private void Start()
    {
        // Mock Initialize Unity Ads
        Debug.Log($"[Monetization] Inicializando SDK Ads com GameID {gameId}. TestMode: {testMode}");
    }

    // Chamado quando o jogador morre e quer reviver
    public void ShowRewardedAdToRevive()
    {
        Debug.Log("[Monetization] A carregar Rewarded Video para Revive...");
        
        onRewardCallback = () => {
            Debug.Log("[Monetization] Vídeo completo. A reviver o jogador!");
            if (playerSanity != null)
            {
                playerSanity.sanity = 20f; // Revive com 20%
                playerSanity.oil = 20f; // E um pouco de óleo
                // Notificar GameManager para reativar o jogo
            }
        };

        // Simula o callback do Ads SDK após 2 segundos
        Invoke("SimulateAdCompletion", 2f);
    }

    // Chamado no menu (Hub) para ganhar moedas de Metaprocessão
    public void ShowRewardedAdForSouls()
    {
        Debug.Log("[Monetization] A carregar Rewarded Video para obter Almas (Souls)...");
        
        onRewardCallback = () => {
            Debug.Log("[Monetization] Vídeo completo. +50 Almas adicionadas à carteira.");
            if (hubManager != null)
            {
                hubManager.AddSouls(50);
            }
        };

        Invoke("SimulateAdCompletion", 2f);
    }

    private void SimulateAdCompletion()
    {
        // Num SDK real, isto seria chamado no OnUnityAdsShowComplete (showCompletionState == COMPLETED)
        if (onRewardCallback != null)
        {
            onRewardCallback.Invoke();
            onRewardCallback = null;
        }
    }

    // IAP: In-App Purchases Mock
    public void PurchasePremiumLantern()
    {
        Debug.Log("[IAP] Pagamento processado (Mock). Lampião Premium desbloqueado.");
        // MetaSaveSystem guardaria a flag de compra
    }

    public void PurchaseRemoveAds()
    {
        Debug.Log("[IAP] Pagamento processado (Mock). Anúncios desativados permanentemente.");
    }
}
