// WebGL 平台下该编辑器冒烟工具不参与编译（其强引用的 EventSystems 在切平台增量编译时偶发缺失，且与网页构建无关）；其余平台保持可用。
#if UNITY_EDITOR && !UNITY_WEBGL
using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using PixelToCivilization.Core;

namespace PixelToCivilization.EditorTools
{
    /// <summary>
    /// V601 runtime smoke test for batchmode (-executeMethod):
    /// empty scene auto boot -> start new game -> build buildings -> tick 5050 years
    /// covering all 8 eras (triggers OnEra style rebuild) -> detect exceptions,
    /// empty meshes, pink (InternalError/FallbackError) materials.
    /// Exits 0 on [SMOKE_PASS], 1 on [SMOKE_FAIL].
    /// </summary>
    public static class V601SmokeTest
    {
        public static void Run()
        {
            int pinkMat = 0, totalMat = 0, emptyMesh = 0, totalRenderers = 0;
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

                var bootGo = new GameObject("Boot");
                var boot = bootGo.AddComponent<Bootstrap.GameBootstrap>();
                boot.Boot();

                var gm = GameManager.Instance;
                if (gm == null) throw new Exception("GameManager.Instance is null, Boot failed");
                gm.StartNewGame();
                gm.Env.PopulateInitial();
                Debug.Log("[SMOKE] scene + initial forest/population done");

                // UI raycast sanity: fullscreen container layers must NOT intercept pointer raycasts,
                // otherwise IsPointerOverGameObject() is always true and map clicks/camera rotation die.
                if (UnityEngine.Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
                    throw new Exception("EventSystem missing - UI cannot receive clicks");
                var hudGo = GameObject.Find("HUD");
                if (hudGo == null) throw new Exception("HUD not found (should be active after StartNewGame)");
                var hudImg = hudGo.GetComponent<UnityEngine.UI.Image>();
                if (hudImg != null && hudImg.raycastTarget)
                    throw new Exception("HUD fullscreen Image raycastTarget=true - blocks all map clicks");
                var mlGo = GameObject.Find("ModalLayer");
                if (mlGo != null)
                {
                    var mlImg = mlGo.GetComponent<UnityEngine.UI.Image>();
                    if (mlImg != null && mlImg.raycastTarget)
                        throw new Exception("ModalLayer fullscreen Image raycastTarget=true - blocks all map clicks");
                }
                Debug.Log("[SMOKE] UI raycast sanity passed (EventSystem + transparent layers)");

                // Design-doc extension systems: philosophy / disaster / history events / victory
                if (gm.Philosophy == null || gm.Disaster == null || gm.HistoryEvent == null || gm.Victory == null)
                    throw new Exception("design-doc systems not installed (Philosophy/Disaster/HistoryEvent/Victory)");
                gm.Philosophy.Adopt("fa");
                if (gm.State.Philosophy != "fa") throw new Exception("Philosophy.Adopt failed");
                Debug.Log("[SMOKE] design-doc systems installed (philosophy adopted=fa)");

                // Real building placement across categories
                string[] types = { "hut", "farm", "palace", "arrow_tower", "pagoda", "road", "canal",
                                   "watchtower", "great_wall", "skyscraper", "space_elevator" };
                int built = 0;
                foreach (var t in types)
                {
                    for (int k = 0; k < 6; k++)
                        if (gm.Building.FindAutoPosition(t, out var x, out var z) &&
                            gm.Building.PlaceBuilding(t, x, z)) { built++; break; }
                }
                Debug.Log("[SMOKE] real builds=" + built + "/" + types.Length);

                // Tick 5050 years covering all 8 eras (era switch triggers building style rebuild)
                int lastEra = -1;
                var sysFields = typeof(GameManager).GetFields()
                    .Where(f => typeof(GameSystemBase).IsAssignableFrom(f.FieldType)).ToArray();
                for (int year = 0; year < 5060; year++)
                {
                    // 60秒/年：scaled-dt 传 60 恰好推进 1 游戏年（60 * 365/60 = 365天）
                    gm.Time.Tick(60f);
                    foreach (var f in sysFields)
                        (f.GetValue(gm) as GameSystemBase)?.Tick(60f);
                    int era = gm.State.Era;
                    if (era != lastEra)
                    {
                        lastEra = era;
                        Debug.Log("[SMOKE] era index=" + era + " year=" + gm.State.Year);
                    }
                }
                Debug.Log("[SMOKE] year tick done, final era=" + gm.State.Era);
                // Design-doc systems ran through 5050 years: history events fired, corruption grew
                Debug.Log("[SMOKE] history events fired=" + gm.State.FiredEvents.Count +
                          " corruption=" + Mathf.RoundToInt(gm.State.Corruption) +
                          " monarch=" + (gm.State.MonarchWise ? "wise" : "foolish"));
                if (gm.State.FiredEvents.Count == 0) throw new Exception("no history events fired in 5050 years");

                // Render asset health check
                foreach (var r in UnityEngine.Object.FindObjectsOfType<Renderer>(true))
                {
                    totalRenderers++;
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null && mf.sharedMesh.vertexCount == 0) emptyMesh++;
                    foreach (var m in r.sharedMaterials)
                    {
                        if (m == null) continue;
                        totalMat++;
                        if (m.shader == null || m.shader.name.Contains("InternalError") ||
                            m.shader.name.Contains("FallbackError"))
                        {
                            pinkMat++;
                            Debug.LogWarning("[SMOKE] pink material: " + r.name +
                                             " shader=" + (m.shader ? m.shader.name : "null"));
                        }
                    }
                }
                Debug.Log("[SMOKE] Renderers=" + totalRenderers + " materials=" + totalMat +
                          " pink=" + pinkMat + " emptyMesh=" + emptyMesh);

                if (pinkMat > 0) throw new Exception("pink materials: " + pinkMat);
                if (emptyMesh > 0) throw new Exception("empty meshes: " + emptyMesh);
                if (built < types.Length)
                    Debug.LogWarning("[SMOKE] some types not built (tech/resource gated, non-fatal)");

                Debug.Log("[SMOKE_PASS] V601 runtime smoke all passed");
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError("[SMOKE_FAIL] " + e);
                EditorApplication.Exit(1);
            }
        }
    }
}
#endif
