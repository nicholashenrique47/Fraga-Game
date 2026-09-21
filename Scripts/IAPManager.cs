using UnityEngine;
using System;
using System.IO;

/// IAPManager: Gestao de In-App Purchases para o modelo Freemium.
/// Estruturado para integrar com Unity IAP (com.unity.purchasing).
/// Em modo de desenvolvimento usa mocks; em producao, descomentar as linhas marcadas [PROD].
///
/// Produtos definidos:
///   - fraga_souls_100:     100 Almas (Tier 1)
///   - fraga_souls_500:     500 Almas (Tier 2)
///   - fraga_souls_2000:    2000 Almas (Tier 3 - Melhor valor)
///   - fraga_remove_ads:    Remover Anuncios (Non-Consumable)
///   - fraga_premium_lamp:  Lampiao Premium Cosmetic (Non-Consumable)

[System.Serializable]
public class IAPSaveData
{
    public bool adsRemoved = false;
    public bool premiumLampOwned = false;
}

public class IAPManager : MonoBehaviour
{
    public static IAPManager Instance { get; private set; }

    [Header("Dependencies")]
    public HubManager hubManager;
    public MonetizationManager monetizationManager;

    [Header("Product IDs")]
    public string souls100Id = "fraga_souls_100";
    public string souls500Id = "fraga_souls_500";
    public string souls2000Id = "fraga_souls_2000";
    public string removeAdsId = "fraga_remove_ads";
    public string premiumLampId = "fraga_premium_lamp";

    // Estado persistente de compras nao-consumiveis
    private IAPSaveData iapData;
    private static string iapSavePath;

    // Eventos para a UI
    public event Action<string> OnPurchaseSuccess;
    public event Action<string> OnPurchaseFailed;

    public bool AdsRemoved => iapData != null && iapData.adsRemoved;
    public bool PremiumLampOwned => iapData != null && iapData.premiumLampOwned;

    private void Awake()
    {
        Instance = this;
        iapSavePath = Application.persistentDataPath + "/fraga_iap.dat";
        LoadIAPData();
        InitializeStore();
    }

    private void InitializeStore()
    {
        // [PROD] Descomentar quando o package Unity IAP estiver instalado:
        // var builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance());
        // builder.AddProduct(souls100Id, ProductType.Consumable);
        // builder.AddProduct(souls500Id, ProductType.Consumable);
        // builder.AddProduct(souls2000Id, ProductType.Consumable);
        // builder.AddProduct(removeAdsId, ProductType.NonConsumable);
        // builder.AddProduct(premiumLampId, ProductType.NonConsumable);
        // UnityPurchasing.Initialize(this, builder);

        Debug.Log("[IAPManager] Loja inicializada (Mock Mode).");
    }

    /// Inicia a compra de um produto. Em mock, simula sucesso imediato.
    public void PurchaseProduct(string productId)
    {
        Debug.Log($"[IAPManager] Processando compra: {productId}");

        // [PROD] Substituir por:
        // m_StoreController.InitiatePurchase(productId);

        // Mock: simular sucesso
        ProcessPurchase(productId);
    }

    /// Processa a compra apos confirmacao do SDK.
    /// Em producao, este metodo seria chamado pelo callback IStoreListener.ProcessPurchase.
    private void ProcessPurchase(string productId)
    {
        bool success = false;

        if (productId == souls100Id)
        {
            if (hubManager != null) hubManager.AddSouls(100);
            success = true;
            Debug.Log("[IAPManager] +100 Almas adquiridas.");
        }
        else if (productId == souls500Id)
        {
            if (hubManager != null) hubManager.AddSouls(500);
            success = true;
            Debug.Log("[IAPManager] +500 Almas adquiridas.");
        }
        else if (productId == souls2000Id)
        {
            if (hubManager != null) hubManager.AddSouls(2000);
            success = true;
            Debug.Log("[IAPManager] +2000 Almas adquiridas (Melhor Valor).");
        }
        else if (productId == removeAdsId)
        {
            iapData.adsRemoved = true;
            SaveIAPData();

            // Notificar MonetizationManager para parar de mostrar ads
            if (monetizationManager != null)
            {
                monetizationManager.testMode = true; // Desativa ads
            }

            success = true;
            Debug.Log("[IAPManager] Anuncios removidos permanentemente.");
        }
        else if (productId == premiumLampId)
        {
            iapData.premiumLampOwned = true;
            SaveIAPData();
            success = true;
            Debug.Log("[IAPManager] Lampiao Premium desbloqueado.");
        }

        if (success)
        {
            if (OnPurchaseSuccess != null) OnPurchaseSuccess.Invoke(productId);
        }
        else
        {
            if (OnPurchaseFailed != null) OnPurchaseFailed.Invoke(productId);
        }
    }

    /// Restaura compras nao-consumiveis (obrigatorio para iOS).
    public void RestorePurchases()
    {
        Debug.Log("[IAPManager] Restaurando compras...");

        // [PROD]:
        // if (Application.platform == RuntimePlatform.IPhonePlayer ||
        //     Application.platform == RuntimePlatform.OSXPlayer)
        // {
        //     var apple = m_StoreExtensionProvider.GetExtension<IAppleExtensions>();
        //     apple.RestoreTransactions((result) => {
        //         Debug.Log("[IAPManager] Restauracao: " + (result ? "Sucesso" : "Falhou"));
        //     });
        // }

        // Mock: restaurar do disco local
        LoadIAPData();
        Debug.Log($"[IAPManager] Estado restaurado: AdsRemoved={iapData.adsRemoved}, PremiumLamp={iapData.premiumLampOwned}");
    }

    // Persistencia binaria
    private void SaveIAPData()
    {
        try
        {
            using (FileStream stream = new FileStream(iapSavePath, FileMode.Create))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(1); // Versao
                writer.Write(iapData.adsRemoved);
                writer.Write(iapData.premiumLampOwned);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[IAPManager] Falha ao gravar: {e.Message}");
        }
    }

    private void LoadIAPData()
    {
        iapData = new IAPSaveData();

        if (!File.Exists(iapSavePath)) return;

        try
        {
            using (FileStream stream = new FileStream(iapSavePath, FileMode.Open))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                int version = reader.ReadInt32();
                iapData.adsRemoved = reader.ReadBoolean();
                iapData.premiumLampOwned = reader.ReadBoolean();
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[IAPManager] Falha ao carregar IAP data: {e.Message}");
            iapData = new IAPSaveData();
        }
    }
}
