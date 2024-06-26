/*
 Author: Simona Hiadlovsk�
 Amount of changes: 5% changed - Added workaround for locking race condition
 Edited by: Josef Kn�e
*/

using System.Collections.Generic;
using UnityEngine;
using System;
using System.Threading.Tasks;
using Base;

public abstract class HInteractiveObject : MonoBehaviour
{
    // Start is called before the first frame update
    public bool IsLocked { get; protected set; }
    public string LockOwner { get; protected set; }

    protected bool lockedTree = false; //when object is locked, is also locked the whole tree?

    public bool IsLockedByMe => IsLocked && LockOwner == HLandingManager.Instance.GetUsername();
    public bool IsLockedByOtherUser => IsLocked && LockOwner != HLandingManager.Instance.GetUsername();

    public bool Blocklisted => blocklisted;

    public List<Collider> Colliders = new List<Collider>();


    /// <summary>
    /// Indicates that object is on blacklist and should not be listed in aim menu and object visibility should be 0
    /// </summary>
    private bool blocklisted;

    public bool Enabled = true;

    protected virtual void Start() {
        HLockingEventCache.Instance.OnObjectLockingEvent += OnObjectLockingEvent;

    }

    public virtual void DisplayOffscreenIndicator(bool active) {

    }

    // ONDESTROY CANNOT BE USED BECAUSE OF ITS DELAYED CALL - it causes mess when directly creating project from scene
    public virtual void DestroyObject() {
        HLockingEventCache.Instance.OnObjectLockingEvent -= OnObjectLockingEvent;
    }

    protected string GetLockedText() {
        return "LOCKED by " + LockOwner + "\n" + GetName();
    }

    public abstract string GetName();
    public abstract string GetId();

    public abstract string GetObjectTypeName();

    public abstract Task<Base.RequestResult> Movable();
    public abstract void StartManipulation();

    public abstract Task<Base.RequestResult> Removable();
    public abstract void Remove();

    public virtual float GetDistance(Vector3 origin) {
        float minDist = float.MaxValue;
        foreach (Collider collider in Colliders) {
            Vector3 point = collider.ClosestPointOnBounds(origin);

            minDist = Math.Min(Vector3.Distance(origin, point), minDist);

        }
        return minDist;
    }

    /// <summary>
    /// Sets wheter or not the object is enabled for interaction in the scene
    /// Note: putOnBlocklist and removeFromBlocklist could not be both true!
    /// Note2: Could not set enable to true and putOnBlocklist at the same time!
    /// </summary>
    /// <param name="enable">Enable flag</param>
    /// <param name="putOnBlocklist">Object should be blocklisted (if it is not already)</param>
    /// <param name="removeFromBlocklist">Object should be removed from blacklist</param>
    public virtual void Enable(bool enable, bool putOnBlocklist = false, bool removeFromBlocklist = false) {
        Debug.Assert(!(putOnBlocklist && removeFromBlocklist));
        Debug.Assert(!(putOnBlocklist && enable));

        if (blocklisted && !removeFromBlocklist) 
            return;
        if (putOnBlocklist) {
            blocklisted = true;
            PlayerPrefsHelper.SaveBool($"ActionObject/{GetId()}/blocklisted", true);
        }
        if (removeFromBlocklist) {
            blocklisted = false;

            PlayerPrefsHelper.SaveBool($"ActionObject/{GetId()}/blocklisted", false);
        }
        Enabled = enable;
        UpdateColor();

        foreach (Collider collider in Colliders) {
            collider.enabled = enable;
        }
       
    }

    public abstract void UpdateColor();

    public abstract Task Rename(string name);

    /// <summary>
    /// Locks object. If successful - returns true, if not - shows notification and returns false.
    /// </summary>
    /// <param name="lockTree">Lock also tree? (all levels of parents and children)</param>
    /// <returns></returns>
    public virtual async Task<bool> WriteLock(bool lockTree) {
        if (IsLockedByMe) { //object is already locked by this user
            if (lockedTree != lockTree) {
                /*if (await UpdateLock(lockTree ? IO.Swagger.Model.UpdateLockRequestArgs.NewTypeEnum.TREE : IO.Swagger.Model.UpdateLockRequestArgs.NewTypeEnum.OBJECT)) {
                    lockedTree = lockTree;
                    return true;
                } // if updateLock failed, try to lock normally*/
            } else { //same type of lock
                return true;
            }
        }

        try {
            await WebSocketManagerH.Instance.WriteLock(GetId(), lockTree);
            lockedTree = lockTree;
            LockByMe();
            return true;
        } catch (RequestFailedException ex) {
            await WriteUnlock();
            return false;
        }
    }

    public void LockByMe()
    {
        IsLocked = true;
        LockOwner = HLandingManager.Instance.GetUsername();
    }

    /// <summary>
    /// Unlocks object. 
    /// If successful - returns true, if not - returns false.
    /// </summary>
    /// <returns></returns>
    public virtual async Task<bool> WriteUnlock() {
        if (!IsLocked)
        {
            return true;
        }

        try {
            await WebSocketManagerH.Instance.WriteUnlock(GetId());
            IsLocked = false;
            return true;
        } catch (RequestFailedException ex) {
            Debug.LogError(ex.Message);
            return false;
        }
    }

    protected virtual void OnObjectLockingEvent(object sender, ObjectLockingEventArgs args) {
        if (!args.ObjectIds.Contains(GetId()))
            return;

        if (args.Locked) {
            OnObjectLocked(args.Owner);
        } else {
            OnObjectUnlocked();
        }
    }

    public virtual void OnObjectUnlocked() {
        IsLocked = false;
        UpdateColor();
    }

    public virtual void OnObjectLocked(string owner) {
        IsLocked = true;
        LockOwner = owner;
    
    }

    public virtual void EnableOffscreenIndicator(bool enable) {
       // offscreenIndicator.enabled = enable;
    }

    public abstract void EnableVisual(bool enable);
}
