using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 右下角显示当前弹匣 / 备弹。运行时按名称查找 PlayerWeaponAttack，避免编译期依赖该类（消除 CS0246 导致的脚本失效与 Broken PPtr）。
/// </summary>
[DisallowMultipleComponent]
public class PlayerAmmoHud : MonoBehaviour
{
    const string AttackTypeName = "PlayerWeaponAttack";

    MonoBehaviour _weaponAttackCache;
    PropertyInfo _propMag;
    PropertyInfo _propReserve;

    Text _label;

    void Awake()
    {
        ResolveWeaponAttack();
        BuildHud();
    }

    void ResolveWeaponAttack()
    {
        foreach (var mb in GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mb != null && mb.GetType().Name == AttackTypeName)
            {
                _weaponAttackCache = mb;
                var t = mb.GetType();
                _propMag = t.GetProperty("AmmoInMagazine", BindingFlags.Instance | BindingFlags.Public);
                _propReserve = t.GetProperty("ReserveAmmo", BindingFlags.Instance | BindingFlags.Public);
                break;
            }
        }
    }

    void BuildHud()
    {
        var canvasGo = new GameObject("AmmoHudCanvas");
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGo.AddComponent<GraphicRaycaster>();

        var panel = new GameObject("AmmoPanel");
        panel.transform.SetParent(canvasGo.transform, false);
        var prt = panel.AddComponent<RectTransform>();
        prt.anchorMin = new Vector2(1f, 0f);
        prt.anchorMax = new Vector2(1f, 0f);
        prt.pivot = new Vector2(1f, 0f);
        prt.anchoredPosition = new Vector2(-28f, 28f);
        prt.sizeDelta = new Vector2(260f, 56f);

        var textGo = new GameObject("AmmoText");
        textGo.transform.SetParent(panel.transform, false);
        _label = textGo.AddComponent<Text>();
        _label.fontSize = 26;
        _label.color = Color.white;
        _label.alignment = TextAnchor.MiddleRight;
        _label.supportRichText = false;
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font != null)
            _label.font = font;

        var trt = textGo.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;

        var outline = textGo.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);
    }

    void LateUpdate()
    {
        if (_label == null)
            return;

        if (_weaponAttackCache == null || _propMag == null || _propReserve == null)
            ResolveWeaponAttack();

        if (_weaponAttackCache == null || _propMag == null || _propReserve == null)
        {
            _label.text = "";
            return;
        }

        var magObj = _propMag.GetValue(_weaponAttackCache);
        var resObj = _propReserve.GetValue(_weaponAttackCache);
        int mag = magObj is int mi ? mi : 0;
        int res = resObj is int ri ? ri : 0;
        _label.text = $"{mag} / {res}";
    }
}
