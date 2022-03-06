using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IO.Swagger.Model;
using UnityEngine;
using UnityEngine.Networking;
using WebSocketSharp;
using Base;
using Hololens;

public class SceneManagerH :  Singleton<SceneManagerH>
{

    /// <summary>
    /// Invoked when new scene loaded
    /// </summary>
    public event EventHandler OnLoadScene;

    /// <summary>
    /// Invoked when scene chagned
    /// </summary>
    public event EventHandler OnSceneChanged;

    /// <summary>
    /// Flag which indicates whether scene update event should be trigered during update
    /// </summary>
    private bool updateScene = false;

    /// <summary>
    /// Spawn point for new action objects. Typically scene origin.
    /// </summary>
    public GameObject ActionObjectsSpawn;

    /// <summary>
    /// Indicates if action objects should be interactable in scene (if they should response to clicks)
    /// </summary>
    public bool ActionObjectsInteractive;
    /// <summary>
    /// Indicates visibility of action objects in scene
    /// </summary>
    public float ActionObjectsVisibility;
    /// <summary>
    /// Indicates if robots end effector should be visible
    /// </summary>
    public bool RobotsEEVisible {
        get;
        private set;
    }

    /// <summary>
    /// Invoked when robot and EE are selected (can be opened robot stepping menu)
    /// </summary>
    public event EventHandler OnRobotSelected;

    /// <summary>
    /// Object which is currently selected in scene
    /// </summary>
    public GameObject CurrentlySelectedObject;

    /// <summary>
    /// Prefab of connectino between action point and action object
    /// </summary>
    public GameObject LineConnectionPrefab;


    /// <summary>
    /// Manager taking care of connections between action points and action objects
    /// </summary>
    public LineConnectionsManager AOToAPConnectionsManager;

    /// <summary>
    /// Invoked when robor should show their EE pose
    /// </summary>
    public event EventHandler OnShowRobotsEE;
    /// <summary>
    /// Invoked when robots should hide their EE pose
    /// </summary>
    public event EventHandler OnHideRobotsEE;
    /// <summary>
    /// Indicates if resources (e.g. end effectors for robot) should be loaded when scene created.
    /// </summary>
    private bool loadResources = false;

    /// <summary>
    /// Prefab for robot action object
    /// </summary>        
    public GameObject RobotPrefab;

    /// Origin (0,0,0) of scene.
    /// </summary>
    public GameObject SceneOrigin;
    /// <summary>
    /// Prefab for action object
    /// </summary>
    public GameObject ActionObjectPrefab;
    /// <summary>
    /// Prefab for action object without pose
    /// </summary>
    public GameObject ActionObjectNoPosePrefab;
    /// <summary>
    /// Prefab for collision object
    /// </summary>
    public GameObject CollisionObjectPrefab;
    public bool Valid = false;
    /// <summary>
    /// Indicates whether or not scene was changed since last save
    /// </summary>
    private bool sceneChanged = false;

    /// <summary>
    /// Defines if scene was started on server - e.g. if all robots and other action objects
    /// are instantioned and are ready
    /// </summary>
        private bool sceneStarted = false;

    public HIRobot SelectedRobot;

    /// <summary>
    /// Prefab for robot end effector object
    /// </summary>
    public GameObject RobotEEPrefab;

    public string SelectedArmId;

    private HRobotEE selectedEndEffector;

    /// <summary>
    /// Holds all action objects in scene
    /// </summary>
    public Dictionary<string, ActionObjectH> ActionObjects = new Dictionary<string, ActionObjectH>();
   
    public event AREditorEventArgs.SceneStateHandler OnSceneStateEvent;

    /// <summary>
    /// Contains metainfo about scene (id, name, modified etc) without info about objects and services
    /// </summary>
    public Scene SceneMeta = null;


    /// <summary>
    /// Public setter for sceneChanged property. Invokes OnSceneChanged event with each change and
    /// OnSceneSavedStatusChanged when sceneChanged value differs from original value (i.e. when scene
    /// was not changed and now it is and vice versa)
    /// </summary>
    public bool SceneChanged {
        get => sceneChanged;
        set {
            bool origVal = SceneChanged;
            sceneChanged = value;
            if (!Valid)
                return;
            OnSceneChanged?.Invoke(this, EventArgs.Empty);
            if (origVal != value) {
              //  OnSceneSavedStatusChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }
    // Start is called before the first frame update
    void Start()
    {

        WebSocketManagerH.Instance.OnSceneStateEvent += OnSceneState;
        //OnLoadScene += OnSceneLoaded;
    }

    // Update is called once per frame
    void Update()
    {
        if (updateScene) {
            SceneChanged = true;
            updateScene = false;
        }
    }

    /// <summary>
    /// Initialize robots end effectors
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OnSceneLoaded(object sender, EventArgs e) {
       /* if (RobotsEEVisible) {
            ShowRobotsEE();
        }*/
    }


    public bool SceneStarted {
        get => sceneStarted;
        private set => sceneStarted = value;
    }
    public HRobotEE SelectedEndEffector {
        get => selectedEndEffector;
        set => selectedEndEffector = value;
    }

    private async void OnSceneState(object sender, SceneStateEventArgs args) {
        switch (args.Event.State) {
            case SceneStateData.StateEnum.Starting:
           //     GameManager.Instance.ShowLoadingScreen("Going online...");
                OnSceneStateEvent?.Invoke(this, args); // needs to be rethrown to ensure all subscribers has updated data
                break;
            case SceneStateData.StateEnum.Stopping:
                SceneStarted = false;
               // GameManager.Instance.ShowLoadingScreen("Going offline...");
          /*      if (!args.Event.Message.IsNullOrEmpty()) {
                    Notifications.Instance.ShowNotification("Scene service failed", args.Event.Message);
                }*/
                OnSceneStateEvent?.Invoke(this, args); // needs to be rethrown to ensure all subscribers has updated data
                break;
            case SceneStateData.StateEnum.Started:
                StartCoroutine(WaitUntillSceneValid(() => OnSceneStarted(args)));
                break;
            case SceneStateData.StateEnum.Stopped:
                SceneStarted = false;
           //     GameManager.Instance.HideLoadingScreen();
                SelectedRobot = null;
                SelectedArmId = null;
                SelectedEndEffector = null;
                OnSceneStateEvent?.Invoke(this, args); // needs to be rethrown to ensure all subscribers has updated data
            /*    if (RobotsEEVisible)
                    OnHideRobotsEE?.Invoke(this, EventArgs.Empty);*/
                break;
        }
    }

    /// <summary>
    /// Register or unregister to/from subsription of joints or end effectors pose of each robot in the scene.
    /// </summary>
    /// <param name="send">To subscribe or to unsubscribe</param>
    /// <param name="what">Pose of end effectors or joints</param>
    public void RegisterRobotsForEvent(bool send, RegisterForRobotEventRequestArgs.WhatEnum what) {
        foreach (HIRobot robot in GetRobots()) {
            WebSocketManagerH.Instance.RegisterForRobotEvent(robot.GetId(), send, what);
        }
    }
    
    private IEnumerator WaitUntillSceneValid(UnityEngine.Events.UnityAction callback) {
        yield return new WaitUntil(() => Valid);
        callback();
    }



    /// <summary>
    /// Finds action object by ID or throws KeyNotFoundException.
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public ActionObjectH GetActionObject(string id) {
        if (ActionObjects.TryGetValue(id, out ActionObjectH actionObject))
            return actionObject;
        throw new KeyNotFoundException("Action object not found");
    }
    
    /// <summary>
    /// Returns all robots in scene
    /// </summary>
    /// <returns></returns>
    public List<HIRobot> GetRobots() {
        List<HIRobot> robots = new List<HIRobot>();
        foreach (ActionObjectH actionObject in ActionObjects.Values) {
            if (actionObject.IsRobot()) {
                robots.Add((RobotActionObjectH) actionObject);
            }                    
        }
        return robots;
    }

    
    private async void OnSceneStarted(SceneStateEventArgs args) {
        SceneStarted = true;
     //   if (RobotsEEVisible)
         //   OnShowRobotsEE?.Invoke(this, EventArgs.Empty);
        RegisterRobotsForEvent(true, RegisterForRobotEventRequestArgs.WhatEnum.Joints);
        string selectedRobotID = PlayerPrefsHelper.LoadString(SceneMeta.Id + "/selectedRobotId", null);
        SelectedArmId = PlayerPrefsHelper.LoadString(SceneMeta.Id + "/selectedRobotArmId", null);
        string selectedEndEffectorId = PlayerPrefsHelper.LoadString(SceneMeta.Id + "/selectedEndEffectorId", null);
        await SelectRobotAndEE(selectedRobotID, SelectedArmId, selectedEndEffectorId);
    //    GameManager.Instance.HideLoadingScreen();
        OnSceneStateEvent?.Invoke(this, args); // needs to be rethrown to ensure all subscribers has updated data
    }

        public async Task SelectRobotAndEE(string robotId, string armId, string eeId) {
        if (!string.IsNullOrEmpty(robotId)) {
            try {
                HIRobot robot = GetRobot(robotId);
                if (!string.IsNullOrEmpty(eeId)) {
                    try {
                        SelectRobotAndEE(await (robot.GetEE(eeId, armId)));
                    } catch (ItemNotFoundException ex) {
                        PlayerPrefsHelper.SaveString(SceneMeta.Id + "/selectedEndEffectorId", null);
                        PlayerPrefsHelper.SaveString(SceneMeta.Id + "/selectedRobotArmId", null);
                        Debug.LogError(ex);
                    }
                }
            } catch (ItemNotFoundException ex) {
                PlayerPrefsHelper.SaveString(SceneMeta.Id + "/selectedRobotId", null);
                Debug.LogError(ex);
            }
        } else {
            SelectRobotAndEE(null);
        }
    }

    public void SelectRobotAndEE(HRobotEE endEffector) {
        if (endEffector == null) {
            SelectedArmId = null;
            SelectedRobot = null;
            SelectedEndEffector = null;
            PlayerPrefsHelper.SaveString(SceneMeta.Id + "/selectedRobotId", null);
            PlayerPrefsHelper.SaveString(SceneMeta.Id + "/selectedRobotArmId", null);
            PlayerPrefsHelper.SaveString(SceneMeta.Id + "/selectedEndEffectorId", null);
        } else {
            try {
                SelectedArmId = endEffector.ARMId;
                SelectedRobot = GetRobot(endEffector.Robot.GetId());
                SelectedEndEffector = endEffector;
            } catch (ItemNotFoundException ex) {
                PlayerPrefsHelper.SaveString(SceneMeta.Id + "/selectedRobotId", null);
                Debug.LogError(ex);
            }

            PlayerPrefsHelper.SaveString(SceneMeta.Id + "/selectedRobotId", SelectedRobot.GetId());
            PlayerPrefsHelper.SaveString(SceneMeta.Id + "/selectedRobotArmId", SelectedArmId);
            PlayerPrefsHelper.SaveString(SceneMeta.Id + "/selectedEndEffectorId", SelectedEndEffector.EEId);
        }

        OnRobotSelected(this, EventArgs.Empty);
    }

    /// <summary>
    /// Gets robot based on its ID
    /// </summary>
    /// <param name="robotId">UUID of robot</param>
    /// <returns></returns>
    public HIRobot GetRobot(string robotId) {
        foreach (HIRobot robot in GetRobots()) {
            if (robot.GetId() == robotId)
                return robot;
        }
        throw new ItemNotFoundException("No robot with id: " + robotId);
    }




    /// <summary>
    /// Creates scene from given json
    /// </summary>
    /// <param name="scene">Json describing scene.</param>
    /// <param name="loadResources">Indicates if resources should be loaded from server.</param>
    /// <param name="customCollisionModels">Allows to override collision models with different ones. Usable e.g. for
    /// project running screen.</param>
    /// <returns>True if scene successfully created, false otherwise</returns>
    public async Task<bool> CreateScene(IO.Swagger.Model.Scene scene, bool loadResources, CollisionModels customCollisionModels = null) {
       
        Debug.Assert(ActionsManagerH.Instance.ActionsReady);
       
        if (SceneMeta != null){
             HNotificationManager.Instance.ShowNotification(SceneMeta.ToString());
            return false;
        }
        else {
             HNotificationManager.Instance.ShowNotification("SCENE META NULL");
        }
           
      //  SelectorMenu.Instance.Clear();
        SetSceneMeta(DataHelper.SceneToBareScene(scene));            
        this.loadResources = loadResources;
     
        LoadSettings();
   
        UpdateActionObjects(scene, customCollisionModels);
        if (scene.Modified == System.DateTime.MinValue) { //new scene, never saved
            sceneChanged = true;
        } else if (scene.IntModified == System.DateTime.MinValue) {
            sceneChanged = false;
        } else {
            sceneChanged = scene.IntModified > scene.Modified;
        }
        Valid = true;
        OnLoadScene?.Invoke(this, EventArgs.Empty);
        return true;
    }


    /// <summary>
    /// Loads selected setings from player prefs
    /// </summary>
    internal void LoadSettings() {
        ActionObjectsVisibility = PlayerPrefsHelper.LoadFloat("AOVisibilityVR", 1f /*+ (VRModeManager.Instance.VRModeON ? "VR" : "AR"), (VRModeManager.Instance.VRModeON ? 1f : 0f)*/);
        ActionObjectsInteractive = PlayerPrefsHelper.LoadBool("scene/" + SceneMeta.Id + "/AOInteractivity", true);
        RobotsEEVisible = PlayerPrefsHelper.LoadBool("scene/" + SceneMeta.Id + "/RobotsEEVisibility", true);
    }

    /// <summary>
    /// Sets scene metadata
    /// </summary>
    /// <param name="scene">Scene metadata</param>
    public void SetSceneMeta(BareScene scene) {
        if (SceneMeta == null) {
            SceneMeta = new Scene(id: "", name: "");
        }
        SceneMeta.Id = scene.Id;
        SceneMeta.Description = scene.Description;
        SceneMeta.IntModified = scene.IntModified;
        SceneMeta.Modified = scene.Modified;
        SceneMeta.Name = scene.Name;
    }


    
    /// <summary>
    /// Updates action GameObjects in ActionObjects dict based on the data present in IO.Swagger.Model.Scene Data.
    /// </summary>
    /// <param name="scene">Scene description</param>
    /// <param name="customCollisionModels">Allows to override action object collision model</param>
    /// <returns></returns>
    public void UpdateActionObjects(Scene scene, CollisionModels customCollisionModels = null) {
        List<string> currentAO = new List<string>();
        foreach (IO.Swagger.Model.SceneObject aoSwagger in scene.Objects) {
            ActionObjectH actionObject = SpawnActionObject(aoSwagger, customCollisionModels);
            actionObject.ActionObjectUpdate(aoSwagger);
            currentAO.Add(aoSwagger.Id);
        }

    }


    /// <summary>
    /// Sets selected object
    /// </summary>
    /// <param name="obj">Object which is currently selected</param>
    public void SetSelectedObject(GameObject obj) {
        if (CurrentlySelectedObject != null) {
            CurrentlySelectedObject.SendMessage("Deselect");
        }
        if (obj != null) {
            obj.SendMessage("OnSelected", SendMessageOptions.DontRequireReceiver);
        }
        CurrentlySelectedObject = obj;
    }

    /// <summary>
    /// Destroys scene and all objects
    /// </summary>
    /// <returns>True if scene successfully destroyed, false otherwise</returns>
    public bool DestroyScene() {
        SceneStarted = false;
        Valid = false;
        RemoveActionObjects();
    //    SelectorMenu.Instance.SelectorItems.Clear();
        SceneMeta = null;
         HNotificationManager.Instance.ShowNotification("DESTORY");
        return true;
    }

    /// <summary>
    /// Destroys and removes references to all action objects in the scene.
    /// </summary>
    public void RemoveActionObjects() {
        foreach (string actionObjectId in ActionObjects.Keys.ToList<string>()) {
            RemoveActionObject(actionObjectId);
        }
        // just to make sure that none reference left
        ActionObjects.Clear();
    }

    /// <summary>
    /// Destroys and removes references to action object of given Id.
    /// </summary>
    /// <param name="Id">Action object ID</param>
    public void RemoveActionObject(string Id) {
        try {
            ActionObjects[Id].DeleteActionObject();
        } catch (NullReferenceException e) {
            Debug.LogError(e);
        }
    }

    /// <summary>
    /// Spawns new action object
    /// </summary>
    /// <param name="id">UUID of action object</param>
    /// <param name="type">Action object type</param>
    /// <param name="customCollisionModels">Allows to override collision model of spawned action objects</param>
    /// <returns>Spawned action object</returns>
    public ActionObjectH SpawnActionObject(IO.Swagger.Model.SceneObject sceneObject, CollisionModels customCollisionModels = null) {
        if (!ActionsManagerH.Instance.ActionObjectsMetadata.TryGetValue(sceneObject.Type, out ActionObjectMetadataH aom)) {
            return null;
        }
        GameObject obj;
        if (aom.Robot) {
            //Debug.Log("URDF: spawning RobotActionObject");
            obj = Instantiate(RobotPrefab, ActionObjectsSpawn.transform);
        } else if (aom.CollisionObject) {
            obj = Instantiate(CollisionObjectPrefab, ActionObjectsSpawn.transform);
        } else if (aom.HasPose) {
            obj = Instantiate(ActionObjectPrefab, ActionObjectsSpawn.transform);
        } else {
            obj = Instantiate(ActionObjectNoPosePrefab, ActionObjectsSpawn.transform);
        }
        ActionObjectH actionObject = obj.GetComponent<ActionObjectH>();
        actionObject.InitActionObject(sceneObject, obj.transform.localPosition, obj.transform.localRotation, aom, customCollisionModels);
        // Add the Action Object into scene reference
        ActionObjects.Add(sceneObject.Id, actionObject);
        actionObject.SetVisibility(ActionObjectsVisibility);
        actionObject.ActionObjectUpdate(sceneObject);
        return actionObject;
    }

    public List<ActionObjectH> GetAllObjectsOfType(string type) {
        return ActionObjects.Values.Where(obj => obj.ActionObjectMetadata.Type == type).ToList();
    }

    /// <summary>
    /// Adds action object to scene
    /// </summary>
    /// <param name="sceneObject">Description of action object</param>
    /// <returns></returns>
    public void SceneObjectAdded(SceneObject sceneObject) {
     /*   ActionObjectH actionObject = SpawnActionObject(sceneObject);
        if (!string.IsNullOrEmpty(SelectCreatedActionObject) && sceneObject.Name.Contains(SelectCreatedActionObject)) {
            if (ActionObjectPickerMenu.Instance.IsVisible())
                AREditorResources.Instance.LeftMenuScene.AddButtonClick();
            SelectorMenu.Instance.SetSelectedObject(actionObject.SelectorItem, true);
            if (!actionObject.ActionObjectMetadata.HasPose)
                SelectorMenu.Instance.BottomButtons.SelectButton(SelectorMenu.Instance.BottomButtons.Buttons[2], true);
        }
        if (OpenTransformMenuOnCreatedObject) {
            AREditorResources.Instance.LeftMenuScene.SetActiveSubmenu(LeftMenuSelection.Utility);
            AREditorResources.Instance.LeftMenuScene.MoveClick();
        }
        SelectCreatedActionObject = "";
        OpenTransformMenuOnCreatedObject = false;
        updateScene = true;*/
    }

    /// <summary>
    /// Removes action object from scene
    /// </summary>
    /// <param name="sceneObject">Description of action object</param>
    public void SceneObjectRemoved(SceneObject sceneObject) {
        ActionObjectH actionObject = GetActionObject(sceneObject.Id);
        if (actionObject != null) {
            ActionObjects.Remove(sceneObject.Id);
            actionObject.DeleteActionObject();
        } else {
            Debug.LogError("Object " + sceneObject.Name + "(" + sceneObject.Id + ") not found");
        }
        updateScene = true;
    }

    /// <summary>
    /// Updates action object in scene
    /// </summary>
    /// <param name="sceneObject">Description of action object</param>
    public void SceneObjectUpdated(SceneObject sceneObject) {
        ActionObjectH actionObject = GetActionObject(sceneObject.Id);
        if (actionObject != null) {
            actionObject.ActionObjectUpdate(sceneObject);
        } else {
            Debug.LogError("Object " + sceneObject.Name + "(" + sceneObject.Id + ") not found");
        }
        SceneChanged = true;
    }

        /// <summary>
    /// Updates metadata of action object in scene
    /// </summary>
    /// <param name="sceneObject">Description of action object</param>
    public void SceneObjectBaseUpdated(SceneObject sceneObject) {
        ActionObjectH actionObject = GetActionObject(sceneObject.Id);
        if (actionObject != null) {

        } else {
            Debug.LogError("Object " + sceneObject.Name + "(" + sceneObject.Id + ") not found");
        }
        updateScene = true;
    }

}
