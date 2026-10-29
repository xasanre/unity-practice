using UnityEngine;
using UnityEngine.EventSystems;

public class BuildSite : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    public Turret turretPrefab;
    public int cost = 100;
    public Turret heavyPrefab;
    public int heavyCost = 150;
    public int SelectedCost => GameManager.Instance != null && GameManager.Instance.heavySelected ? heavyCost : cost;
    public Turret BuiltTower { get; private set; }
    Renderer pad;
    MaterialPropertyBlock colors;
    bool hovered;
    void Awake() { pad = GetComponentInChildren<Renderer>(); colors = new MaterialPropertyBlock(); }
    public bool TryBuild()
    {
        var game = GameManager.Instance;
        if (game == null || game.Finished) return false;
        if (BuiltTower != null) { game.Notify("This pad already has a turret"); return false; }
        var prefab = game.heavySelected ? heavyPrefab : turretPrefab;
        int price = SelectedCost;
        if (prefab == null) { game.Notify("Turret prefab is missing"); return false; }
        if (!game.TrySpend(price)) { game.Notify($"Not enough gold: need {price}, have {game.gold}"); return false; }
        BuiltTower = Instantiate(prefab, transform.position + Vector3.up * 0.14f, Quaternion.identity, transform);
        game.Notify($"{game.SelectedTowerName} turret built (-{price} gold)");
        return true;
    }
    public void OnPointerClick(PointerEventData eventData)
    { if (eventData.button == PointerEventData.InputButton.Left) TryBuild(); }
    public void OnPointerEnter(PointerEventData eventData) { hovered = true; }
    public void OnPointerExit(PointerEventData eventData) { hovered = false; }
    void Update()
    {
        if (pad == null) return;
        var game = GameManager.Instance;
        Color color = BuiltTower != null ? new Color(0.25f,0.32f,0.36f) :
            game != null && (game.Finished || game.gold < SelectedCost) ? new Color(0.6f,0.27f,0.2f) :
            hovered ? new Color(1f,0.9f,0.4f) : new Color(0.85f,0.65f,0.2f);
        colors.SetColor("_BaseColor",color); pad.SetPropertyBlock(colors);
    }
}


