using System;
using UnityEngine.UI;
using Base;
using System.Collections;
using IO.Swagger.Model;
using UnityEngine;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

public class LeftMenuProject : LeftMenu
{

    public ButtonWithTooltip SetActionPointParentButton, AddActionButton, AddActionButton2, RunButton, RunButton2,
        AddConnectionButton, AddConnectionButton2, BuildPackageButton, AddActionPointUsingRobotButton;

    public GameObject ActionPicker;
    public InputDialog InputDialog;
    public AddNewActionDialog AddNewActionDialog;

    private string apNameAddedByRobot = "", updateAPWithRobotId = "", updateAPWithEE = "", selectAPNameWhenCreated = "";
    System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
    protected override void Update() {
        base.Update();
        if (ProjectManager.Instance.ProjectMeta != null)
            EditorInfo.text = "Project: \n" + ProjectManager.Instance.ProjectMeta.Name;
    }

    protected override void Awake() {
        base.Awake();
        Base.ProjectManager.Instance.OnProjectSavedSatusChanged += OnProjectSavedStatusChanged;
        Base.GameManager.Instance.OnOpenProjectEditor += OnOpenProjectEditor;

        SceneManager.Instance.OnSceneStateEvent += OnSceneStateEvent;

        GameManager.Instance.OnGameStateChanged += OnGameStateChanged;
        GameManager.Instance.OnEditorStateChanged += OnEditorStateChanged;
        SelectorMenu.Instance.OnObjectSelectedChangedEvent += OnObjectSelectedChangedEvent;
    }

    protected override void OnSceneStateEvent(object sender, SceneStateEventArgs args) {
        if (GameManager.Instance.GetGameState() == GameManager.GameStateEnum.ProjectEditor)
            base.OnSceneStateEvent(sender, args);  

    }

    protected void OnEnable() {
        ProjectManager.Instance.OnActionPointAddedToScene += OnActionPointAddedToScene;

    }

    protected void OnDisable() {
        ProjectManager.Instance.OnActionPointAddedToScene -= OnActionPointAddedToScene;
    }

    private async void OnActionPointAddedToScene(object sender, ActionPointEventArgs args) {
        if (!string.IsNullOrEmpty(apNameAddedByRobot) && args.ActionPoint.GetName() == apNameAddedByRobot) {
            try {
                await WebsocketManager.Instance.UpdateActionPointUsingRobot(args.ActionPoint.GetId(), updateAPWithRobotId, updateAPWithEE);
                await WebsocketManager.Instance.AddActionPointOrientationUsingRobot(args.ActionPoint.GetId(), updateAPWithRobotId, updateAPWithEE, "default");
                await WebsocketManager.Instance.AddActionPointJoints(args.ActionPoint.GetId(), updateAPWithRobotId, "default");
            } catch (RequestFailedException ex) {
                Debug.LogError(ex);
                Notifications.Instance.ShowNotification("Failed to initialize AP", "Position, orientation or joints were not loaded for selected robot");
            } finally {
                apNameAddedByRobot = "";
                updateAPWithRobotId = "";
                updateAPWithEE = "";
                GameManager.Instance.HideLoadingScreen();
            }


        } 
        if (selectAPNameWhenCreated.Equals(args.ActionPoint.GetName())) {
            SelectorMenu.Instance.ForceUpdateMenus();
            SelectorMenu.Instance.SetSelectedObject(args.ActionPoint, true);
            selectAPNameWhenCreated = "";
            RenameClick(true);
        }

    }

    protected async override Task UpdateBtns(InteractiveObject obj) {
        try {
            if (CanvasGroup.alpha == 0) {
                previousUpdateDone = true;
                return;
            }
        
            await base.UpdateBtns(obj);
            if (requestingObject || obj == null) {
                SetActionPointParentButton.SetInteractivity(false, "No action point is selected");
                AddActionButton.SetInteractivity(false, "No action point is selected");
                AddActionButton2.SetInteractivity(false, "No action point is selected");
                AddConnectionButton.SetInteractivity(false, "No input / output is selected");
                AddConnectionButton2.SetInteractivity(false, "No input / output is selected");
                RunButton.SetInteractivity(false, "No object is selected");
                RunButton2.SetInteractivity(false, "No object is selected");
            } else if (obj.IsLocked) {
                SetActionPointParentButton.SetInteractivity(false, "Object is locked");
                AddConnectionButton.SetInteractivity(false, "Object is locked");
                AddConnectionButton2.SetInteractivity(false, "Object is locked");
                RunButton.SetInteractivity(false, "Object is locked");
                RunButton2.SetInteractivity(false, "Object is locked");
            } else {
                SetActionPointParentButton.SetInteractivity(obj is ActionPoint3D, "Selected object is not action point");
                AddActionButton.SetInteractivity(obj is ActionPoint3D, "Selected object is not action point");
                AddActionButton2.SetInteractivity(obj is ActionPoint3D, "Selected object is not action point");
                
                AddConnectionButton.SetInteractivity(obj.GetType() == typeof(PuckInput) ||
                    obj.GetType() == typeof(PuckOutput), "Selected object is not input or output of an action");
                AddConnectionButton2.SetInteractivity(obj.GetType() == typeof(PuckInput) ||
                    obj.GetType() == typeof(PuckOutput), "Selected object is not input or output of an action");
                string runBtnInteractivity = null;

                if (obj.GetType() == typeof(Action3D)) {
                    if (!SceneManager.Instance.SceneStarted)
                        runBtnInteractivity = "Scene offline";
                    else if (!string.IsNullOrEmpty(GameManager.Instance.ExecutingAction)) {
                        runBtnInteractivity = "Some action is already excecuted";
                    }
                    RunButton.SetDescription("Execute action");
                    RunButton2.SetDescription("Execute action");
                } else if (obj.GetType() == typeof(StartAction)) {
                    if (!ProjectManager.Instance.ProjectMeta.HasLogic) {
                        runBtnInteractivity = "Project without logic could not be started from editor";
                    } else if (ProjectManager.Instance.ProjectChanged) {
                        runBtnInteractivity = "Project has unsaved changes";
                    }
                    RunButton.SetDescription("Run project");
                    RunButton2.SetDescription("Run project");
                } else {
                    runBtnInteractivity = "Selected object is not action or START";
                }

                RunButton.SetInteractivity(string.IsNullOrEmpty(runBtnInteractivity), runBtnInteractivity);
                RunButton2.SetInteractivity(string.IsNullOrEmpty(runBtnInteractivity), runBtnInteractivity);
            }
            if (!SceneManager.Instance.SceneStarted) {
                AddActionPointUsingRobotButton.SetInteractivity(false, "Scene offline");
            } else if (!SceneManager.Instance.IsRobotAndEESelected()) {
                AddActionPointUsingRobotButton.SetInteractivity(false, "Robot or EE not selected");
            } else {
                AddActionPointUsingRobotButton.SetInteractivity(true);
            }

           

        } finally {
            previousUpdateDone = true;
        }
    }

    protected override void DeactivateAllSubmenus() {
        base.DeactivateAllSubmenus();

        AddActionButton.GetComponent<Image>().enabled = false;
        AddActionButton2.GetComponent<Image>().enabled = false;
        ActionPickerMenu.Instance.Hide();
        ActionParametersMenu.Instance.Hide();
        //ActionPicker.SetActive(false);
    }

    private void OnOpenProjectEditor(object sender, EventArgs eventArgs) {
        if (ProjectManager.Instance.ProjectMeta.HasLogic) {
            RunButton.SetInteractivity(true);
            RunButton2.SetInteractivity(true);
        } else {
            RunButton.SetInteractivity(false, "Project without defined logic could not be run from editor");
            RunButton2.SetInteractivity(false, "Project without defined logic could not be run from editor");
        }
    }

    public void SaveProject() {
        SaveButton.SetInteractivity(false, "Saving project...");
        Base.GameManager.Instance.SaveProject();        
    }

    public async void BuildPackage(string name) {
        try {
            await Base.GameManager.Instance.BuildPackage(name);
            InputDialog.Close();
            Notifications.Instance.ShowToastMessage("Package was built sucessfully.");
        } catch (Base.RequestFailedException ex) {

        }

    }


    public async void RunProject() {
        GameManager.Instance.ShowLoadingScreen("Running project", true);
        try {
            await Base.WebsocketManager.Instance.TemporaryPackage();
            MenuManager.Instance.MainMenu.Close();
        } catch (RequestFailedException ex) {
            Base.Notifications.Instance.ShowNotification("Failed to run temporary package", "");
            Debug.LogError(ex);
            GameManager.Instance.HideLoadingScreen(true);
        }
    }

    public void ShowBuildPackageDialog() {
        InputDialog.Open("Build package",
                         "",
                         "Package name",
                         Base.ProjectManager.Instance.ProjectMeta.Name + DateTime.Now.ToString("yyyy-dd-M--HH-mm-ss"),
                         () => BuildPackage(InputDialog.GetValue()),
                         () => InputDialog.Close());
    }


    private void OnProjectSavedStatusChanged(object sender, EventArgs e) {
       UpdateBuildAndSaveBtns();
    }
    

    public override async void UpdateBuildAndSaveBtns() {
        if (GameManager.Instance.GetGameState() != GameManager.GameStateEnum.ProjectEditor)
            return;
        bool successForce;
        string messageForce;
        BuildPackageButton.SetInteractivity(false, "Loading...");
        SaveButton.SetInteractivity(false, "Loading...");
        CloseButton.SetInteractivity(false, "Loading...");


        if (!ProjectManager.Instance.ProjectChanged) {
            BuildPackageButton.SetInteractivity(true);            
            SaveButton.SetInteractivity(false, "There are no unsaved changes");
        } else {
            BuildPackageButton.SetInteractivity(false, "There are unsaved changes on project");
            stopwatch.Reset();
            stopwatch.Start();
            WebsocketManager.Instance.SaveProject(SaveProjectCallback, true);
            
        }

        (successForce, messageForce) = await GameManager.Instance.CloseProject(true, true);
        CloseButton.SetInteractivity(successForce, messageForce);        
    }

    public void SaveProjectCallback(string _, string response) {
        
        SaveProjectResponse saveProjectResponse = JsonConvert.DeserializeObject<SaveProjectResponse>(response);
        SaveButton.SetInteractivity(saveProjectResponse.Result, saveProjectResponse.Messages.FirstOrDefault());
    }


    public void CopyObjectClick() {
        InteractiveObject selectedObject = SelectorMenu.Instance.GetSelectedObject();
        if (selectedObject is null)
            return;
        if (selectedObject.GetType() == typeof(ActionPoint3D)) {
            ProjectManager.Instance.SelectAPNameWhenCreated = "copy_of_" + selectedObject.GetName();
            WebsocketManager.Instance.CopyActionPoint(selectedObject.GetId(), null);
        } else if (selectedObject is Base.Action action) {
            //
            /*
            Action3D action = (Action3D) selectedObject;
            List<ActionParameter> parameters = new List<ActionParameter>();
            foreach (Base.Parameter p in action.Parameters.Values) {
                parameters.Add(new ActionParameter(p.ParameterMetadata.Name, p.ParameterMetadata.Type, p.Value));
            }
            WebsocketManager.Instance.AddAction(action.ActionPoint.GetId(), parameters, action.ActionProvider.GetProviderId() + "/" + action.Metadata.Name, action.GetName() + "_copy", action.GetFlows());*/

            AddNewActionDialog.InitFromAction(action);
            AddNewActionDialog.Open();
        }
    }

    public async void AddConnectionClick() {
        InteractiveObject selectedObject = SelectorMenu.Instance.GetSelectedObject();
        if (selectedObject is null)
            return;
        if ((selectedObject.GetType() == typeof(PuckInput) ||
                selectedObject.GetType() == typeof(PuckOutput))) {
            if (!await ((InputOutput) selectedObject).Action.WriteLock(false))
                return;
            
            ((InputOutput) selectedObject).OnClick(Clickable.Click.TOUCH);
        }
    }


    public async void AddActionClick() {
        //was clicked the button in favorites or settings submenu?
        Button clickedButton = AddActionButton.Button;
        if (currentSubmenuOpened == LeftMenuSelection.Favorites) {
            clickedButton = AddActionButton2.Button;
        }

        if (!SelectorMenu.Instance.gameObject.activeSelf && !clickedButton.GetComponent<Image>().enabled) { //other menu/dialog opened
            SetActiveSubmenu(currentSubmenuOpened); //close all other opened menus/dialogs and takes care of red background of buttons
        }

        if (clickedButton.GetComponent<Image>().enabled) {
            clickedButton.GetComponent<Image>().enabled = false;
            SelectorMenu.Instance.gameObject.SetActive(true);
            //ActionPicker.SetActive(false);
            ActionPickerMenu.Instance.Hide();
        } else {
            if (await ActionPickerMenu.Instance.Show((Base.ActionPoint) selectedObject)) {
                clickedButton.GetComponent<Image>().enabled = true;
                SelectorMenu.Instance.gameObject.SetActive(false);
            } else {
                Notifications.Instance.ShowNotification("Failed to open action picker", "Could not lock action point");
            }
            
        }
    }



    public void AddActionPointClick() {
        CreateGlobalActionPoint(ProjectManager.Instance.GetFreeAPName("global"));
    }

    public void AddActionPointUsingRobotClick() {
        CreateGlobalActionPointUsingRobot(ProjectManager.Instance.GetFreeAPName("global"),
            SceneManager.Instance.SelectedRobot.GetId(),
            SceneManager.Instance.SelectedEndEffector.GetName());
    }

    private void ShowCreateGlobalActionPointDialog() {
        InputDialog.Open("Create action point",
                         "Type action point name",
                         "Name",
                         ProjectManager.Instance.GetFreeAPName("global"),
                         () => CreateGlobalActionPoint(InputDialog.GetValue()),
                         () => InputDialog.Close());
    }

    private async void CreateGlobalActionPoint(string name) {
        selectAPNameWhenCreated = name;
        bool result = await GameManager.Instance.AddActionPoint(name, "");
        if (result)
            InputDialog.Close();
        else
            selectAPNameWhenCreated = "";
    }

    private void ShowCreateGlobalActionPointUsingRobotDialog() {
        if (!SceneManager.Instance.SceneStarted) {
            Notifications.Instance.ShowNotification("Failed to create new AP", "Only available when online");
            return;
        }
        InputDialog.Open("Create action point using robot",
                         SceneManager.Instance.SelectedRobot.GetName() + "/" + SceneManager.Instance.SelectedEndEffector.GetName(),
                         "Name",
                         ProjectManager.Instance.GetFreeAPName("global"),
                         () => CreateGlobalActionPointUsingRobot(InputDialog.GetValue(),
                                                                 SceneManager.Instance.SelectedRobot.GetId(),
                                                                 SceneManager.Instance.SelectedEndEffector.GetName()),
                         () => InputDialog.Close());
    }

    private async void CreateGlobalActionPointUsingRobot(string name, string robotId, string eeId) {
        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(robotId) || string.IsNullOrEmpty(eeId)) {
            Notifications.Instance.ShowNotification("Failed to create new AP", "Some required parameter is missing");
            return;
        }
        GameManager.Instance.ShowLoadingScreen("Adding AP...");
        updateAPWithEE = eeId;
        updateAPWithRobotId = robotId;
        apNameAddedByRobot = name;
        selectAPNameWhenCreated = name;
        bool result = await GameManager.Instance.AddActionPoint(name, "");
        if (result)
            InputDialog.Close();
        else {
            GameManager.Instance.HideLoadingScreen();
        }
    }


    public override void UpdateVisibility() {
        if (GameManager.Instance.GetGameState() == GameManager.GameStateEnum.ProjectEditor &&
            MenuManager.Instance.MainMenu.CurrentState == DanielLochner.Assets.SimpleSideMenu.SimpleSideMenu.State.Closed) {
            UpdateVisibility(true);
        } else {
            UpdateVisibility(false);
        }
    }

    public async void ShowCloseProjectDialog() {
        (bool success, _) = await Base.GameManager.Instance.CloseProject(false);
        if (!success) {
            GameManager.Instance.HideLoadingScreen();
            ConfirmationDialog.Open("Close project",
                         "Are you sure you want to close current project? Unsaved changes will be lost.",
                         () => CloseProject(),
                         () => ConfirmationDialog.Close());
        }

    }

    public async void CloseProject() {
        GameManager.Instance.ShowLoadingScreen("Closing project..");
        _ = await GameManager.Instance.CloseProject(true);
        ConfirmationDialog.Close();
        MenuManager.Instance.MainMenu.Close();
        GameManager.Instance.HideLoadingScreen();
    }

    public async void RunClicked() {
        try {
            InteractiveObject selectedObject = SelectorMenu.Instance.GetSelectedObject();
            if (selectedObject is null)
                return;
            if (selectedObject is StartAction) {
                Debug.LogError("START");
                RunProject();
            } else if (selectedObject is Action3D action) {
                action.ActionBeingExecuted = true;
                await WebsocketManager.Instance.ExecuteAction(selectedObject.GetId(), false);
                // TODO: enable stop execution (_ = GameManager.Instance.CancelExecution();)
                action.ActionBeingExecuted = false;
            } else if (selectedObject.GetType() == typeof(APOrientation)) {
                
                //await WebsocketManager.Instance.MoveToActionPointOrientation(SceneManager.Instance.SelectedRobot.GetId(), SceneManager.Instance.SelectedEndEffector.GetId(), 0.5m, selectedObject.GetId(), false);
            } 
        } catch (RequestFailedException ex) {
            Notifications.Instance.ShowNotification("Failed to execute action", ex.Message);
            return;
        }
        
    }
}
