using System.Collections.Generic;
using NUnit.Framework;
using SBGames.Cameras;
using UnityEngine;

namespace SBGames.Tests
{
    [TestFixture]
    public class CameraController2DTests
    {
        private List<GameObject> _spawnedObjects;

        [SetUp]
        public void SetUp()
        {
            _spawnedObjects = new List<GameObject>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_spawnedObjects != null)
            {
                for (int i = 0; i < _spawnedObjects.Count; i++)
                {
                    if (_spawnedObjects[i] != null)
                    {
                        Object.DestroyImmediate(_spawnedObjects[i]);
                    }
                }
                _spawnedObjects.Clear();
            }
        }

        private GameObject Track(GameObject go)
        {
            _spawnedObjects.Add(go);
            return go;
        }

        [Test]
        public void CameraController2D_Awake_RegistersInstanceAndBindsCamera()
        {
            var camGo = Track(new GameObject("TestCamera"));
            var camera = camGo.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 25f;

            var controller = camGo.AddComponent<CameraController2D>();

            Assert.IsNotNull(CameraController2D.Instance);
            Assert.AreEqual(controller, CameraController2D.Instance);
            Assert.IsFalse(controller.IsDragging);
            Assert.IsFalse(controller.HasDraggedThisPress);
        }

        [Test]
        public void CameraController2D_DuplicateInstance_IsDestroyed()
        {
            var camGo1 = Track(new GameObject("TestCamera1"));
            camGo1.AddComponent<Camera>();
            var controller1 = camGo1.AddComponent<CameraController2D>();

            var camGo2 = Track(new GameObject("TestCamera2"));
            camGo2.AddComponent<Camera>();
            var controller2 = camGo2.AddComponent<CameraController2D>();

            Assert.AreEqual(controller1, CameraController2D.Instance);
        }

        [Test]
        public void CameraController2D_BoundsClamping_ConstrainsPositionWithinDungeonBounds()
        {
            var camGo = Track(new GameObject("TestCamera"));
            var camera = camGo.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 20f;

            var controller = camGo.AddComponent<CameraController2D>();

            // Position far outside dungeon bounds (e.g. 500, 500)
            camGo.transform.position = new Vector3(500f, 500f, -10f);

            var clampMethod = typeof(CameraController2D).GetMethod("ClampPosition", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(clampMethod, "ClampPosition method must exist");
            clampMethod.Invoke(controller, null);

            Rect bounds = controller.GetCurrentBounds();
            Assert.LessOrEqual(camGo.transform.position.x, bounds.xMax + controller.BoundsPadding.x + 0.1f);
            Assert.LessOrEqual(camGo.transform.position.y, bounds.yMax + controller.BoundsPadding.y + 0.1f);
            Assert.GreaterOrEqual(camGo.transform.position.x, bounds.xMin - controller.BoundsPadding.x - 0.1f);
            Assert.GreaterOrEqual(camGo.transform.position.y, bounds.yMin - controller.BoundsPadding.y - 0.1f);
        }

        [Test]
        public void CameraController2D_Clamping_PreventsPanningIntoNegativeVoid()
        {
            var camGo = Track(new GameObject("TestCamera"));
            var camera = camGo.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 35f;

            var controller = camGo.AddComponent<CameraController2D>();
            camGo.transform.position = new Vector3(-500f, -500f, -10f);

            var clampMethod = typeof(CameraController2D).GetMethod("ClampPosition", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(clampMethod, "ClampPosition method must exist");
            clampMethod.Invoke(controller, null);

            Rect bounds = controller.GetCurrentBounds();
            Assert.GreaterOrEqual(camGo.transform.position.x, bounds.xMin - controller.BoundsPadding.x - 0.01f);
            Assert.GreaterOrEqual(camGo.transform.position.y, bounds.yMin - controller.BoundsPadding.y - 0.01f);
        }
    }
}
