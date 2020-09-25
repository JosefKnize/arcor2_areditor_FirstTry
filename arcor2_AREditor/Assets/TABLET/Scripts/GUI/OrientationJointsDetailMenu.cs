using UnityEngine;
using DanielLochner.Assets.SimpleSideMenu;
using Base;
using IO.Swagger.Model;
using System.Globalization;
using Michsky.UI.ModernUIPack;
using UnityEngine.UI;
using System.Collections.Generic;
using System;

[RequireComponent(typeof(SimpleSideMenu))]
public class OrientationJointsDetailMenu : MonoBehaviour, IMenu {
    public Base.ActionPoint CurrentActionPoint;

    public GameObject OrientationBlock, OrientationExpertModeBlock, JointsBlock, JointsExpertModeBlock, MoveHereBlock;

    public OrientationManualEdit OrientationManualEdit;

    public Slider SpeedSlider;

    [SerializeField]
    private TooltipContent updateButtonTooltip, manualOrientationEditTooltip, manualJointsEditTooltip;

    [SerializeField]
    private Button UpdateButton, ManualOrientationEditButton, ManualJointsEditButton;

    [SerializeField]
    private TMPro.TMP_InputField DetailName; //name of current orientation/joints
    [SerializeField]
    private TMPro.TMP_Text RobotName; //name of robot - only for joints


    public DropdownParameter RobotsList, EndEffectorList; //only for orientation

    public GameObject JointsDynamicList;

    public ConfirmationDialog ConfirmationDialog;

    private SimpleSideMenu SideMenu;
    private NamedOrientation orientation;
    private ProjectRobotJoints joints;
    private bool isOrientationDetail; //true for orientation, false for joints

    private void Start() {
        SideMenu = GetComponent<SimpleSideMenu>();
        WebsocketManager.Instance.OnActionPointOrientationUpdated += OnActionPointOrientationUpdated;
        WebsocketManager.Instance.OnActionPointJointsUpdated += OnActionPointJointsUpdated;
    }

    private void OnActionPointJointsUpdated(object sender, RobotJointsEventArgs args) {
        if (joints != null && joints.Id == args.Data.Id) {
            joints = args.Data;
            UpdateMenu();
        }
    }

    private void OnActionPointOrientationUpdated(object sender, ActionPointOrientationEventArgs args) {
         if (orientation != null && orientation.Id == args.Data.Id) {
            orientation = args.Data;
            UpdateMenu();
         }
     }


    public async void UpdateMenu() {
        if (isOrientationDetail) {  //orientation

            DetailName.text = orientation.Name;

            RobotsList.Dropdown.dropdownItems.Clear();
            await RobotsList.gameObject.GetComponent<DropdownRobots>().Init(OnRobotChanged, true);
            if (RobotsList.Dropdown.dropdownItems.Count > 0) {
                OrientationBlock.SetActive(true);
                MoveHereBlock.SetActive(true);

                UpdateButton.interactable = true;
                updateButtonTooltip.enabled = false;

                OnRobotChanged((string) RobotsList.GetValue());
            } else {
                OrientationBlock.SetActive(false);
                MoveHereBlock.SetActive(false);

                updateButtonTooltip.description = "There is no robot to update orientation with";
                updateButtonTooltip.enabled = true;
                UpdateButton.interactable = false;
            }

            OrientationManualEdit.SetOrientation(orientation.Orientation);
            ValidateFieldsOrientation();
        } else { //joints
            DetailName.text = joints.Name;
            UpdateJointsList();
        }
    }


    private async void OnRobotChanged(string robot_name) {
        EndEffectorList.Dropdown.dropdownItems.Clear();

        try {
            string robotId = SceneManager.Instance.RobotNameToId(robot_name);
            await EndEffectorList.gameObject.GetComponent<DropdownEndEffectors>().Init(robotId, null);

        } catch (ItemNotFoundException ex) {
            Debug.LogError(ex);
            Notifications.Instance.ShowNotification("Failed to load end effectors", "");
        }
    }

    /// <summary>
    /// Updates values (angles) of joints in expert block
    /// </summary>
    public void UpdateJointsList() {
        foreach (RectTransform o in JointsDynamicList.GetComponentsInChildren<RectTransform>()) {
            if (!o.gameObject.CompareTag("Persistent")) {
                Destroy(o.gameObject);
            }
        }

        foreach (IO.Swagger.Model.Joint joint in joints.Joints) {
            LabeledInput labeledInput = Instantiate(GameManager.Instance.LabeledFloatInput, JointsDynamicList.transform).GetComponent<LabeledInput>();
            labeledInput.SetLabel(joint.Name, joint.Name);
            labeledInput.SetValue(joint.Value);
        }
    }

    public async void OnJointsSaveClick() {
        List<IO.Swagger.Model.Joint> updatedJoints = new List<IO.Swagger.Model.Joint>();
        try {
            foreach (LabeledInput input in JointsDynamicList.GetComponentsInChildren<LabeledInput>()) {
                decimal value = decimal.Parse(input.Input.text, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
                updatedJoints.Add(new IO.Swagger.Model.Joint(input.GetName(), value));
            }

            await WebsocketManager.Instance.UpdateActionPointJoints(joints.Id, updatedJoints);

        } catch (RequestFailedException ex) {
            Notifications.Instance.ShowNotification("Joints update failed", ex.Message);
            return;
        } catch (Exception ex) { //decimal parsing exceptions
            Notifications.Instance.ShowNotification("Incorrect joint value", ex.Message);
            return;
        }
    }

    public async void OnOrientationSaveClick() {
        try {
            await WebsocketManager.Instance.UpdateActionPointOrientation(OrientationManualEdit.GetOrientation(), orientation.Id);
            Notifications.Instance.ShowNotification("Orientation updated", "");
        } catch (RequestFailedException ex) {
            Notifications.Instance.ShowNotification("Failed to update orientation", ex.Message);
        }
    }


    public async void UpdateUsingRobot() {
        if (isOrientationDetail)
        {
            try {
                string robotId = SceneManager.Instance.RobotNameToId((string) RobotsList.GetValue());
                await WebsocketManager.Instance.UpdateActionPointOrientationUsingRobot(robotId, (string) EndEffectorList.GetValue(), orientation.Id);
            } catch (ItemNotFoundException ex) {
                Debug.LogError(ex);
                Notifications.Instance.ShowNotification("Failed update orientation", ex.Message);
            } catch (RequestFailedException ex) {
                Notifications.Instance.ShowNotification("Failed to update orientation", ex.Message);
            }
        }
        else //joints
        {
            try {
                await WebsocketManager.Instance.UpdateActionPointJointsUsingRobot(joints.Id);
            } catch (RequestFailedException ex) {
                Notifications.Instance.ShowNotification("Failed to update joints", ex.Message);
            }
        }
        ConfirmationDialog.Close();
        UpdateMenu();
    }

    public async void Delete() {
        try {
            if (isOrientationDetail) {
                await WebsocketManager.Instance.RemoveActionPointOrientation(orientation.Id);
            } else {
                await WebsocketManager.Instance.RemoveActionPointJoints(joints.Id);
            }
            ConfirmationDialog.Close();
            Close();

        } catch (RequestFailedException e) {
            Notifications.Instance.ShowNotification("Failed delete orientation/joints", e.Message);
        }
    }

    public void ShowDeleteDialog() {
        string title = isOrientationDetail ? "Delete orientation" : "Delete joints";
        string description = "Do you want to delete " + (isOrientationDetail ? "orientation " : "joints ") + (isOrientationDetail ? orientation.Name : joints.Name) + "?"; 
        ConfirmationDialog.Open(title,
                                description,
                                () => Delete(),
                                () => ConfirmationDialog.Close());
    }

    /// <summary>
    /// Shows confirmation dialog for updating orientation/joints using robot
    /// </summary>
    public void ShowUpdateUsingRobotDialog() {
        string title = isOrientationDetail ? "Update orientation" : "Update joints";
        string description = "Do you want to update ";
        if (isOrientationDetail) {
            description += "orientation using robot: " + (string) RobotsList.GetValue() + " and end effector: " + (string) EndEffectorList.GetValue() + "?";
        } else {
            description += "joints using robot: " + RobotName.text + "?";
        }
        ConfirmationDialog.Open(title,
                                description,
                                () => UpdateUsingRobot(),
                                () => ConfirmationDialog.Close());
    }

    public async void Rename() {
        try {
            string name = DetailName.text;
            
            if (isOrientationDetail) {
                if (name == orientation.Name) {
                    return;
                }
                await WebsocketManager.Instance.RenameActionPointOrientation(orientation.Id, name);
                Notifications.Instance.ShowNotification("Orientation renamed successfully", "");
            } else {
                if (name == joints.Name) {
                    return;
                }
                await WebsocketManager.Instance.RenameActionPointJoints(joints.Id, name);
                Notifications.Instance.ShowNotification("Joints renamed successfully", "");
            }
        } catch (RequestFailedException ex) {
            Notifications.Instance.ShowNotification("Failed to rename orientation/joints", ex.Message);
            UpdateMenu();
        }
    }

    public async void MoveHereRobot() {
        try {
            if (isOrientationDetail) {
                string robotId = SceneManager.Instance.RobotNameToId((string) RobotsList.GetValue());
                await WebsocketManager.Instance.MoveToActionPointOrientation(robotId, (string) EndEffectorList.GetValue(), (decimal) SpeedSlider.value, orientation.Id);
            } else {
                await WebsocketManager.Instance.MoveToActionPointJoints(joints.RobotId, (decimal) SpeedSlider.value, joints.Id);
            }
        } catch (ItemNotFoundException ex) {
            Notifications.Instance.ShowNotification("Failed to move robot", ex.Message);
        } catch (RequestFailedException ex) {
            Notifications.Instance.ShowNotification("Failed to move robot", ex.Message);
        }
    }

    public async void MoveHereModel() {
        //TODO 
        Notifications.Instance.ShowNotification("Not implemented yet", "");
    }

    public async void ValidateFieldsOrientation() {
        bool interactable = true;

        manualOrientationEditTooltip.description = OrientationManualEdit.ValidateFields();
        if (!string.IsNullOrEmpty(manualOrientationEditTooltip.description)) {
            interactable = false;
        }

        manualOrientationEditTooltip.enabled = !interactable;
        ManualOrientationEditButton.interactable = interactable;
    }


    public void Close() {
        CurrentActionPoint.GetGameObject().SendMessage("Select", false);
        SideMenu.Close();
    }


    public void ShowMenu(Base.ActionPoint currentActionPoint, NamedOrientation orientation) {
        this.orientation = orientation;
        this.isOrientationDetail = true;

        ShowMenu(currentActionPoint);
    }

    public void ShowMenu(Base.ActionPoint currentActionPoint, ProjectRobotJoints joints) {
        this.joints = joints;
        isOrientationDetail = false;
        try {
            RobotName.text = SceneManager.Instance.GetRobot(joints.RobotId).GetName();
        } catch (ItemNotFoundException ex) {
            Notifications.Instance.ShowNotification(ex.Message, "");
        }
        ShowMenu(currentActionPoint);
    }

    private void ShowMenu(Base.ActionPoint actionPoint) {
        CurrentActionPoint = actionPoint;

        OrientationBlock.SetActive(isOrientationDetail);
        OrientationExpertModeBlock.SetActive(isOrientationDetail && GameManager.Instance.ExpertMode);
        JointsBlock.SetActive(!isOrientationDetail);
        JointsExpertModeBlock.SetActive(!isOrientationDetail && GameManager.Instance.ExpertMode);


        UpdateMenu();
        SideMenu.Open();
    }
}