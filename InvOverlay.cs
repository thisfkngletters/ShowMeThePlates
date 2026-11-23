using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using BepInEx.Configuration;
using EFT.InventoryLogic;
using EFT.UI.DragAndDrop;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace ShowMeThePlates
{
    // Texto de durabilidad de placas / insertos (chalecos, cascos, mascaras con insertos, pierneras) sobre el icono del objeto en el inventario + tooltip
    public partial class Plugin
    {
        public static Plugin Inst;
        ConfigEntry<float> cInvBgAlpha; ConfigEntry<bool> cInvBg, cInvText, cInvTip, cInvFull, cInvClass, cInvClassIcon;
        ConfigEntry<float> cInvSize;
        const string InvName = "SMTP_InvText";
        static Item hovered;

        class Part { public string Short, Long; public float Cur, Max; public int Cls, Rank; public bool Empty, ClassOnly; }

        void InvInit()
        {
            const string I = "8. Inventory";
            cInvText = Config.Bind(I, "Show durability on item icon", true, new ConfigDescription("Shows each plate / helmet insert durability over the vest or helmet icon in the inventory, stash and loot.", null, new ConfigurationManagerAttributes { Order = 50 }));
            cInvTip = Config.Bind(I, "Show durability in tooltip", true, new ConfigDescription("Adds the plate / insert list to the item tooltip when hovering.", null, new ConfigurationManagerAttributes { Order = 49 }));
            cInvFull = Config.Bind(I, "Show max durability", false, new ConfigDescription("On the icon: 'F 56/60' instead of 'F 56'.", null, new ConfigurationManagerAttributes { Order = 48 }));
            cInvClass = Config.Bind(I, "Show armor class", true, new ConfigDescription("Shows the armor class (I-VI) of each plate / insert, e.g. [VI] F. Plate, in the tooltip, and the class badge on ballistic mask icons.", null, new ConfigurationManagerAttributes { Order = 46 }));
            cInvClassIcon = Config.Bind(I, "Armor class on icon", true, new ConfigDescription("Also shows the armor class badge (the game's own icon) before each line on the item icon.", null, new ConfigurationManagerAttributes { Order = 45 }));
            cInvBg = Config.Bind(I, "Dark background", true, new ConfigDescription("Semi-transparent black box behind the text on the item icon.", null, new ConfigurationManagerAttributes { Order = 44 }));
            cInvBgAlpha = Config.Bind(I, "Background opacity", 0.4f, new ConfigDescription("Opacity of the dark box.", new AcceptableValueRange<float>(0f, 1f), new ConfigurationManagerAttributes { Order = 43 }));
            cInvSize = Config.Bind(I, "Icon text size (%)", 100f, new ConfigDescription("Scale of the text on the item icon.", new AcceptableValueRange<float>(50f, 250f), new ConfigurationManagerAttributes { Order = 47 }));

            var h = new Harmony("com.showmetheplates.inv");
            int n = 0;
            foreach (var t in new[] { typeof(GridItemView), typeof(SlotItemView) })
                n += PatchDeclared(h, t, "UpdateInfo", "InfoPost", null);
            foreach (var t in new[] { typeof(ItemView), typeof(GridItemView), typeof(SlotItemView) })
            {
                n += PatchDeclared(h, t, "OnPointerEnter", "EnterPost", null);
                n += PatchDeclared(h, t, "OnPointerExit", "ExitPost", null);
            }
            MethodInfo show = null;
            foreach (var m in typeof(EFT.UI.SimpleTooltip).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (m.Name != "Show") continue;
                var ps = m.GetParameters();
                if (ps.Length > 0 && ps[0].ParameterType == typeof(string)) { show = m; break; }
            }
            if (show != null) { h.Patch(show, prefix: new HarmonyMethod(typeof(Plugin).GetMethod("TipPre", BindingFlags.Static | BindingFlags.NonPublic)), postfix: new HarmonyMethod(typeof(Plugin).GetMethod("TipPost", BindingFlags.Static | BindingFlags.NonPublic))); n++; }
            Dbg("Inventario: " + n + " parches aplicados");
        }

        int PatchDeclared(Harmony h, Type t, string name, string post, string pre)
        {
            var m = t.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly, null, name == "OnPointerEnter" || name == "OnPointerExit" ? new[] { typeof(UnityEngine.EventSystems.PointerEventData) } : Type.EmptyTypes, null);
            if (m == null) return 0;
            h.Patch(m, postfix: new HarmonyMethod(typeof(Plugin).GetMethod(post, BindingFlags.Static | BindingFlags.NonPublic)));
            return 1;
        }

        // Placas / insertos de un objeto (vacio si no es blindaje con piezas).
        // soft = true: si NO tiene ranuras de placa (ni piezas de casco), lista sus insertos blandos (chalecos solo con insertos; icono y tooltip).
        // Chalecos con ranuras de placa: solo placas (si estan vacias, nada), para no saturar el icono (flea market).
        static List<Part> PartsOf(Item item, bool soft = false, bool noPlates = false, bool faceClass = false)
        {
            var list = new List<Part>(); List<Part> ins = null; bool plateSlot = false;
            // faceClass = true (solo el icono): mascaras blindadas (FaceCover) -> solo la insignia de clase, sin durabilidad (el juego ya la muestra)
            if (faceClass && IsFaceCover(item))
            {
                int fc = item.GetItemComponent<RepairableComponent>() != null ? ClassOf(item) : 0;
                if (fc > 0) list.Add(new Part { Short = "", Long = "Armor class", Cls = fc, ClassOnly = true });
            }
            var ci = item as CompoundItem;
            if (ci == null) return list;
            foreach (var sl in ci.Slots)
            {
                string low = (sl.ID ?? "").ToLowerInvariant();
                if (low.Contains("plate") && !low.Contains("soft")) plateSlot = true;   // tiene ranuras de placa (llenas o vacias)
                var ch = sl.ContainedItem;
                if (ch == null) continue;
                var rep = ch.GetItemComponent<RepairableComponent>();
                if (rep == null) continue;
                if (low.Contains("plate") && !low.Contains("soft"))
                {
                    string lb = LabelFor(low);
                    list.Add(new Part { Short = lb.Replace(" Plate", "").Replace(".", ""), Long = lb, Cur = rep.Durability, Max = rep.MaxDurability, Cls = ClassOf(ch) });
                    continue;
                }
                int z = low.StartsWith("helmet") ? ZoneFor(low) : -1;
                if (z >= 0) { list.Add(new Part { Short = ZoneShort[z], Long = ZoneNames[z], Cur = rep.Durability, Max = rep.MaxDurability, Cls = ClassOf(ch) }); continue; }
                if (IsVisor(sl, ch)) { list.Add(new Part { Short = "G", Long = "Gear", Cur = rep.Durability, Max = rep.MaxDurability, Cls = ClassOf(ch) }); continue; }
                if (soft && !low.StartsWith("mod_") && !low.StartsWith("helmet") && ch.GetItemComponent<ArmorComponent>() != null)
                {
                    int rk; string full; string lb = InsertLabel(low, sl.ID ?? "", out rk, out full);
                    if (ins == null) ins = new List<Part>();
                    ins.Add(new Part { Short = rk < SoftShort.Length ? SoftShort[rk] : lb, Long = lb, Cur = rep.Durability, Max = rep.MaxDurability, Cls = ClassOf(ch), Rank = rk });
                }
            }
            // noPlates = true (solo el icono): ranuras de placa todas vacias -> una linea "No Plates"
            if (noPlates && list.Count == 0 && plateSlot) { list.Add(new Part { Short = "No Plates", Long = "No Plates", Empty = true }); return list; }
            if (!plateSlot && ins != null && (list.Count == 0 || list[0].ClassOnly)) { ins.Sort((a, b) => a.Rank.CompareTo(b.Rank)); list.AddRange(ins); return list; }
            return list;
        }
        // Mascara (slot FaceCover): plantilla hija de "FaceCover" (5a341c4686f77469e155819e)
        const string FaceCoverParent = "5a341c4686f77469e155819e";
        static readonly Dictionary<string, bool> faceCache = new Dictionary<string, bool>();
        static bool IsFaceCover(Item item)
        {
            string tid = item.TemplateId.ToString(); bool r;
            if (faceCache.TryGetValue(tid, out r)) return r;
            r = false;
            try { for (var t = item.Template; t != null; t = t.Parent) if (t.ParentId.HasValue && t.ParentId.Value.ToString() == FaceCoverParent) { r = true; break; } }
            catch { r = false; }
            faceCache[tid] = r;
            return r;
        }

        // Insignia grafica de clase (sprite TMP del juego "armor_classes_N"); si no existe, numero romano en gris
        static string ClassTag(TextMeshProUGUI t, int c)
        {
            var sa = t.spriteAsset != null ? t.spriteAsset : TMP_Settings.defaultSpriteAsset;
            if (sa != null && sa.GetSpriteIndexFromName("armor_classes_" + c) >= 0) return "<sprite name=\"armor_classes_" + c + "\"> ";
            return "<color=#9A9A9A>" + Rom(c) + "</color> ";
        }
        static bool spriteLogged; static TMP_SpriteAsset cachedSprite;
        static int ClassOf(Item it)
        {
            try
            {
                var ac = it.GetItemComponent<ArmorComponent>();
                if (ac == null) return 0;
                var pi = ac.GetType().GetProperty("ArmorClass");
                return pi != null ? Convert.ToInt32(pi.GetValue(ac, null)) : 0;
            }
            catch { return 0; }
        }
        static readonly string[] Roman = { "", "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X" };
        static string Rom(int c) { return c > 0 && c < Roman.Length ? Roman[c] : ""; }
        static readonly string[] ZoneShort = { "T", "N", "E", "J", "Ea" };
        static readonly string[] SoftShort = { "Cs", "Bs", "LS", "RS", "Ns", "Gs", "GRs", "LSh", "RSh", "LT", "RT", "LC", "RC", "LF", "RF" };   // por rango de InsertLabel

        static string Hex(float frac)
        {
            return frac <= 0.001f ? "C50505" : frac < 0.25f ? "DB7A1C" : frac < 0.5f ? "D9B52B" : "9CD425";
        }

        static void InfoPost(object __instance)
        {
            var inst = Inst; if (inst == null) return;
            try
            {
                var iv = __instance as ItemView;
                if (iv == null) return;
                var tr = iv.transform.Find(InvName);
                var parts = inst.cInvText.Value ? PartsOf(iv.Item, true, true, inst.cInvClass.Value) : null;
                if (parts == null || parts.Count == 0) { if (tr != null) tr.gameObject.SetActive(false); return; }
                TextMeshProUGUI t;
                if (tr == null)
                {
                    var box = new GameObject(InvName, typeof(RectTransform));
                    box.transform.SetParent(iv.transform, false);
                    var brt = (RectTransform)box.transform;
                    brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.zero; brt.pivot = Vector2.zero; brt.anchoredPosition = new Vector2(3f, 3f);
                    var img = box.AddComponent<UnityEngine.UI.Image>(); img.raycastTarget = false; img.color = new Color(0f, 0f, 0f, 0.4f);
                    var lg = box.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
                    lg.padding = new RectOffset((int)PadX, (int)PadX, (int)PadY, (int)PadY);
                    lg.childControlWidth = true; lg.childControlHeight = true; lg.childForceExpandWidth = false; lg.childForceExpandHeight = false;
                    var csf = box.AddComponent<UnityEngine.UI.ContentSizeFitter>();
                    csf.horizontalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize; csf.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
                    var go = new GameObject("T", typeof(RectTransform));
                    go.transform.SetParent(box.transform, false);
                    tr = box.transform;
                    t = go.AddComponent<TextMeshProUGUI>();
                    t.raycastTarget = false;
                    t.enableWordWrapping = false;
                    t.overflowMode = TextOverflowModes.Overflow;
                    t.alignment = TextAlignmentOptions.BottomLeft; t.color = Color.white;
                    t.richText = true;
                    var rt = t.rectTransform;
                    
                    inst.FindTmpFont();
                    if (inst.tmpFont != null) t.font = inst.tmpFont;
                    
                }
                else { tr.gameObject.SetActive(true); t = tr.GetComponentInChildren<TextMeshProUGUI>(true); }
                if (t == null) return;
                float statSize = 0f; TMP_Text stat = null;
                try
                {
                    var bp = iv.BottomPanel;
                    if (bp != null)
                    {
                        foreach (var nm in new[] { "ItemViewStats", "ItemValue" })
                        {
                            var fi = bp.GetType().GetField(nm);
                            var cand = fi != null ? fi.GetValue(bp) as TMP_Text : null;
                            if (cand != null && cand.font != null) { stat = cand; break; }
                        }
                    }
                }
                catch { }
                if (stat != null)
                {
                    if (!fontDone.Contains(t.GetInstanceID()))
                    {
                        t.font = stat.font; t.fontSharedMaterial = stat.fontSharedMaterial; t.fontStyle = stat.fontStyle; t.characterSpacing = stat.characterSpacing;
                        if (stat.spriteAsset != null) t.spriteAsset = stat.spriteAsset;
                        fontDone.Add(t.GetInstanceID());
                        if (!fontLogged) { fontLogged = true; inst.Dbg("Inventario: fuente copiada de '" + stat.name + "' = " + stat.font.name + " tam " + stat.fontSize + " estilo " + stat.fontStyle); }
                    }
                    statSize = stat.fontSize;
                    cachedSprite = t.spriteAsset != null ? t.spriteAsset : TMP_Settings.defaultSpriteAsset;
                    if (!spriteLogged)
                    {
                        spriteLogged = true;
                        var sa = t.spriteAsset != null ? t.spriteAsset : TMP_Settings.defaultSpriteAsset;
                        inst.Dbg("Inventario: spriteAsset = " + (sa != null ? sa.name + ", armor_classes_6 idx " + sa.GetSpriteIndexFromName("armor_classes_6") : "(ninguno)"));
                    }
                }
                float h = ((RectTransform)iv.transform).rect.height;
                t.fontSize = (statSize > 0f ? statSize : Mathf.Clamp(h * 0.115f, 7f, 26f)) * inst.cInvSize.Value / 100f;
                var sb = new StringBuilder();
                bool full = inst.cInvFull.Value;
                for (int i = 0; i < parts.Count; i++)
                {
                    var q = parts[i];
                    float f = q.Max > 0f ? q.Cur / q.Max : 0f;
                    if (i > 0) sb.Append('\n');
                    if (q.Empty) { sb.Append("<color=#9A9A9A>").Append(q.Short).Append("</color>"); continue; }
                    if (q.ClassOnly) { sb.Append(inst.cInvClassIcon.Value ? ClassTag(t, q.Cls).TrimEnd() + "\u00A0" : "<color=#9A9A9A>Class " + Rom(q.Cls) + "</color>"); continue; }
                    sb.Append(inst.cInvClass.Value && inst.cInvClassIcon.Value && q.Cls > 0 ? ClassTag(t, q.Cls) : "").Append(q.Short).Append(' ').Append("<color=#").Append(Hex(f)).Append('>').Append(Mathf.Floor(q.Cur + 0.0001f).ToString("0")).Append("</color>");
                    if (full) sb.Append('/').Append(Mathf.Floor(q.Max + 0.0001f).ToString("0"));
                }
                t.text = sb.ToString();
                // mascaras (solo insignia): sin texto la caja oscura sale mas pequena que la insignia; se le da el alto de una linea normal
                var le = t.GetComponent<UnityEngine.UI.LayoutElement>();
                bool onlyBadge = parts.Count == 1 && parts[0].ClassOnly;
                if (onlyBadge && le == null) le = t.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
                if (le != null) { le.minHeight = onlyBadge ? t.fontSize * 1.25f : -1f; le.minWidth = onlyBadge ? t.fontSize * 1.6f : -1f; }
                var bimg = tr.GetComponent<UnityEngine.UI.Image>();
                if (bimg != null)
                {
                    bimg.enabled = inst.cInvBg.Value;
                    bimg.color = new Color(0f, 0f, 0f, inst.cInvBgAlpha.Value);
                }
                t.ForceMeshUpdate();
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)tr);
            }
            catch (Exception e) { if (!invErr) { invErr = true; inst.Logger.LogWarning("Inventario(texto): " + e); } }
        }
        const float PadX = 4f, PadY = 2f;
        static bool invErr, fontLogged; static readonly HashSet<int> fontDone = new HashSet<int>();

        static void EnterPost(object __instance)
        {
            var iv = __instance as ItemView;
            hovered = iv != null ? iv.Item : null;
        }
        static void ExitPost(object __instance) { hovered = null; }

        static void TipPost(object __instance)
        {
            try
            {
                var inst = Inst; var c = __instance as Component;
                if (inst == null || c == null || cachedSprite == null || hovered == null || !inst.cInvTip.Value) return;
                foreach (var tx in c.GetComponentsInChildren<TMP_Text>(true))
                    if (tx.spriteAsset != cachedSprite) tx.spriteAsset = cachedSprite;
            }
            catch { }
        }

        static void TipPre(ref string text)
        {
            var inst = Inst; if (inst == null || hovered == null || !inst.cInvTip.Value || text == null) return;
            try
            {
                var parts = PartsOf(hovered, true);
                if (parts.Count == 0 || text.Contains(parts[0].Long + ":")) return;
                var sb = new StringBuilder(text);
                bool spr = cachedSprite != null;
                sb.Append('\n');
                foreach (var q in parts)
                    sb.Append('\n').Append(inst.cInvClass.Value && q.Cls > 0 ? (spr && cachedSprite.GetSpriteIndexFromName("armor_classes_" + q.Cls) >= 0 ? "<sprite name=\"armor_classes_" + q.Cls + "\"> " : "[" + Rom(q.Cls) + "] ") : "").Append(q.Long).Append(": ").Append(Mathf.Floor(q.Cur + 0.0001f).ToString("0")).Append('/').Append(Mathf.Floor(q.Max + 0.0001f).ToString("0"));
                text = sb.ToString();
            }
            catch { }
        }
    }
}
