﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using RData.Ui;
using RData.Ui.Contexts;
using RData.Tools;

namespace RData.Ui.Tracking
{
    /// <summary>
    /// This component is to be placed on the gameobject
    /// to track if it's active or not. This is the base class for
    /// other component-specific trackers
    /// </summary>
    [ExecuteInEditMode]
    public class GameObjectTracker : MonoBehaviour
    {
        [SerializeField][HideInInspector]
        private string _gameObjectGuid;
        
        private GameObjectTracker _parentTracker;

        private GameObjectActiveContext _context;


        public string GameObjectGuid
        {
            get { return _gameObjectGuid; }
        }

        public GameObjectActiveContext Context
        {
            get { return _context; }
        }
        
        private void Reset()
        {
            if(!Application.isPlaying)
                EnsureGuid();
        }

        protected virtual void Awake()
        {
            // Editor
            if (!Application.isPlaying)
            {
                EnsureGuid();

            }
            else // Application.isPlaying
            {
                if (transform.parent != null)
                    _parentTracker = transform.parent.GetComponent<GameObjectTracker>();
            }
        }

        private void EnsureGuid()
        {
            if (string.IsNullOrEmpty(_gameObjectGuid)) // If guid is null or empty, generate a new one
            {
                if (RData.RDataLogging.DoLog)
                    Debug.Log("Generating new guid for " + gameObject.name);
                _gameObjectGuid = Guid.NewGuid().ToString();
            }
            else // If Object was copied and pasted, check if it's guid does not match existing guids
            {
                //  Todo: use scene-based or project-based cache
                var trackers = FindObjectsOfType(typeof(GameObjectTracker));
                foreach (var tracker in (GameObjectTracker[])trackers)
                {
                    if (tracker != this && _gameObjectGuid == tracker.GameObjectGuid)
                    {
                        if (RData.RDataLogging.DoLog)
                            Debug.Log("Regenerating guid for " + gameObject.name);
                        _gameObjectGuid = Guid.NewGuid().ToString();
                    }
                }
            }
        }

        protected void OnEnable()
        {
            if(Application.isPlaying)
                StartGameObjectActiveContext();
        }

        protected void OnDisable()
        {
            if (Application.isPlaying)
                EndGameObjectActiveContext();
        }

        protected void StartGameObjectActiveContext()
        {                        
            _context = new GameObjectActiveContext(GameObjectGuid, gameObject.name, GameObjectHelper.GetGameObjectPath(transform));

            RDataSingleton.Client.StartContext(_context, _parentTracker != null ? _parentTracker.Context : null);
            if (RData.RDataLogging.DoLog)
                Debug.Log("Start context for " + gameObject.name + " parent = " + (_parentTracker == null ? null : _parentTracker.gameObject.name));
        }

        protected void EndGameObjectActiveContext()
        {            
            if (gameObject.activeSelf) // If the object is still active, parent was turned off. Don't send anything.
                return;

            RDataSingleton.Client.EndContext(_context);
            _context = null;
            if (RData.RDataLogging.DoLog)
                Debug.Log("End context for " + gameObject.name);
        }
    }
}