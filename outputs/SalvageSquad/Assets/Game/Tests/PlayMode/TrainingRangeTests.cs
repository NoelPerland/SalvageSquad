using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SalvageSquad.Tests
{
    public class TrainingRangeTests
    {
        [UnityTest]
        public IEnumerator MouseDragMovesSquadAndReleaseStopsAcceptingMotion()
        {
            yield return SceneManager.LoadSceneAsync("TrainingRange");
            yield return null;
            var run = Object.FindFirstObjectByType<PrototypeRun>();
            var previousBackground = InputSystem.settings.backgroundBehavior;
            var previousEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                Vector2 start = new Vector2(Screen.width * .5f, Screen.height * .5f);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = start }.WithButton(MouseButton.Left));
                yield return null;
                yield return null;
                Vector2 end = start + new Vector2(Screen.width * .2f, 0);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = end }.WithButton(MouseButton.Left));
                yield return new WaitForSeconds(.25f);
                Assert.That(run.squad.transform.position.x, Is.GreaterThan(1));
                InputSystem.QueueStateEvent(mouse, new MouseState { position = end });
                yield return null;
                yield return null;
                float goal = run.squad.DesiredX;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = start });
                yield return null;
                yield return null;
                Assert.That(run.squad.DesiredX, Is.EqualTo(goal));
                run.restartButton.onClick.Invoke();
                Assert.That(run.squad.transform.position.x, Is.Zero);
            }
            finally
            {
                InputSystem.RemoveDevice(mouse);
                InputSystem.settings.backgroundBehavior = previousBackground;
                InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInput;
            }
        }

        [UnityTest]
        public IEnumerator TrainingRangeShootsMovesPausesAndResetsWithoutGrowingPools()
        {
            yield return SceneManager.LoadSceneAsync("TrainingRange");
            yield return null;
            var run = Object.FindFirstObjectByType<PrototypeRun>();
            Assert.That(run, Is.Not.Null);
            Assert.That(run.squad.UnitCount, Is.EqualTo(5));
            int poolObjects = run.combat.GetComponentsInChildren<Transform>(true).Length;
            yield return new WaitForSeconds(4);
            Assert.That(run.combat.TotalHits, Is.GreaterThan(0), "Forward shots should hit the first center target.");
            Assert.That(run.combat.Salvaged, Is.GreaterThan(0));
            run.squad.SetDesiredX(99);
            yield return new WaitForSeconds(.4f);
            Assert.That(run.squad.transform.position.x, Is.EqualTo(run.squad.horizontalLimit).Within(.01));
            run.SetPaused(true);
            float elapsed = run.Elapsed;
            yield return new WaitForSeconds(.2f);
            Assert.That(run.Elapsed, Is.EqualTo(elapsed));
            run.SetPaused(false);
            run.squad.SetDesiredX(-2.5f);
            yield return new WaitForSeconds(2);
            CapturePreview();
            for (int i = 0; i < 4; i++)
            {
                run.ResetRun();
                Assert.That(run.combat.ActiveProjectiles, Is.Zero);
                Assert.That(run.combat.ActiveTargets, Is.Zero);
                Assert.That(run.combat.ActiveEffects, Is.Zero);
                Assert.That(run.combat.Salvaged, Is.Zero);
                Assert.That(run.combat.Missed, Is.Zero);
                Assert.That(run.combat.TotalHits, Is.Zero);
                Assert.That(run.squad.transform.position, Is.EqualTo(Vector3.zero));
                Assert.That(run.Elapsed, Is.Zero);
                Assert.That(run.IsPaused, Is.False);
                Assert.That(run.combat.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(poolObjects));
                yield return new WaitForSeconds(.3f);
            }
        }

        static void CapturePreview()
        {
            Camera camera = Camera.main;
            RenderTexture previous = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            var target = new RenderTexture(540, 960, 24);
            var texture = new Texture2D(540, 960, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                Canvas.ForceUpdateCanvases();
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 540, 960), 0, 0);
                texture.Apply();
                File.WriteAllBytes(Path.GetFullPath(Path.Combine(Application.dataPath, "../../Preview.png")), texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previous;
                RenderTexture.active = previousActive;
                Object.Destroy(target);
                Object.Destroy(texture);
            }
        }
    }
}
