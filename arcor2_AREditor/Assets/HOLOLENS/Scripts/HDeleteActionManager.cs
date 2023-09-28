using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Microsoft.MixedReality.Toolkit.UI;
using Base;

public class HDeleteActionManager : Singleton<HDeleteActionManager>
{

    public GameObject deleteActionManager;
    public Interactable outputButton;
    public Interactable actionButton;
    

    private HAction action;
    // Start is called before the first frame update
    void Start()
    {
        actionButton.OnClick.AddListener(() => HSelectorManager.Instance.deleteObject());
        outputButton.OnClick.AddListener(async () => {await  WebSocketManagerH.Instance.RemoveLogicItem(action.Output.GetLogicItems()[0].Data.Id);
                                                             Hide(); });

    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void Show(HAction action){
        deleteActionManager.SetActive(true);
        deleteActionManager.transform.parent = action.transform;
        deleteActionManager.transform.position = action.transform.position;
        this.action = action;

        resetButtons();
     

    }

    public void Hide() {
        deleteActionManager.transform.parent = null;
        deleteActionManager.SetActive(false);
    }


    public void resetButtons(){
      //  inputButton.gameObject.SetActive(false);
        outputButton.gameObject.SetActive(true);
        actionButton.gameObject.SetActive(true);
    }

    public void setActiveActionButton(bool active){
        actionButton.gameObject.SetActive(active);

    }

  /*   public void setActiveInputButton(bool active){
        inputButton.gameObject.SetActive(active);

    }*/

 public void setActiveOutputButton(bool active){
        outputButton.gameObject.SetActive(active);

    }


  
}
