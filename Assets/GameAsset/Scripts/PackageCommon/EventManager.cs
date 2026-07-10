using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wayfu.Lamkn
{
    public class EventManager : MonoBehaviour
    {
        private static EventManager _instance;
        
        public static EventManager Instance 
        {
            get 
            {
                if (_instance == null)
                {
                    var go = new GameObject("EventManager");
                    _instance = go.AddComponent<EventManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }
        
        public delegate void CallBackObserver(object data);
        
        private Dictionary<string, HashSet<CallBackObserver>> _observersByTopic = new Dictionary<string, HashSet<CallBackObserver>>();
        
        // Đảm bảo chỉ có một instance
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        
        // Phương thức thêm listener với null checks
        public void AddEvent(string topicName, CallBackObserver callbackObserver) 
        {
            if (string.IsNullOrEmpty(topicName))
            {
                Debug.LogError("EventManager: Topic name cannot be null or empty");
                return;
            }
            
            if (callbackObserver == null)
            {
                Debug.LogError("EventManager: Callback observer cannot be null");
                return;
            }
            
            GetOrCreateObservers(topicName).Add(callbackObserver);
        }
        
        // Phương thức gói để sử dụng dễ dàng như trước
        public static void AddEventStatic(string topicName, CallBackObserver callbackObserver)
        {
            Instance.AddEvent(topicName, callbackObserver);
        }
        
        // Phương thức xóa listener được cải thiện
        public void RemoveEvent(string topicName, CallBackObserver callbackObserver)
        {
            if (string.IsNullOrEmpty(topicName) || callbackObserver == null)
                return;
                
            if (_observersByTopic.TryGetValue(topicName, out var observers))
            {
                observers.Remove(callbackObserver);
                
                // Clean up empty topics to prevent memory bloat
                if (observers.Count == 0)
                {
                    _observersByTopic.Remove(topicName);
                }
            }
        }
        
        // Phương thức gói để sử dụng dễ dàng như trước
        public static void RemoveEventStatic(string topicName, CallBackObserver callbackObserver)
        {
            Instance.RemoveEvent(topicName, callbackObserver);
        }
        
        // Phương thức thông báo sự kiện được cải thiện để tránh collection modification exception
        public void Notify(string topicName, object data = null)
        {
            if (string.IsNullOrEmpty(topicName))
                return;
                
            if (!_observersByTopic.TryGetValue(topicName, out var observers) || observers.Count == 0)
                return;
            
            // Convert to array to completely avoid collection modification issues
            var observersArray = new CallBackObserver[observers.Count];
            observers.CopyTo(observersArray);
            
            foreach (var observer in observersArray)
            {
                try
                {
                    // Double-check observer still exists in case it was removed during iteration
                    if (observer != null && observers.Contains(observer))
                    {
                        observer.Invoke(data);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"EventManager: Exception in observer for topic '{topicName}': {e.Message}");
                }
            }
        }
        
        // Phương thức gói để sử dụng dễ dàng như trước
        public static void NotifyStatic(string topicName, object data = null)
        {
            Instance.Notify(topicName, data);
        }
        
        // Helper methods
        private HashSet<CallBackObserver> GetOrCreateObservers(string topicName)
        {
            if (!_observersByTopic.TryGetValue(topicName, out var observers))
            {
                observers = new HashSet<CallBackObserver>();
                _observersByTopic[topicName] = observers;
            }
            
            return observers;
        }
        
        // Method to check if a topic has any observers (for cleanup purposes)
        public bool HasObservers(string topicName)
        {
            return _observersByTopic.TryGetValue(topicName, out var observers) && observers.Count > 0;
        }
        
        // Method to get count of observers for a topic (useful for debugging)
        public int GetObserverCount(string topicName)
        {
            return _observersByTopic.TryGetValue(topicName, out var observers) ? observers.Count : 0;
        }
        
        // Method to clear all observers for a specific topic
        public void ClearTopic(string topicName)
        {
            if (string.IsNullOrEmpty(topicName))
                return;
                
            _observersByTopic.Remove(topicName);
        }
        
        // Method to clear all observers (useful for cleanup)
        public void ClearAll()
        {
            _observersByTopic.Clear();
        }
        
        // Method to get all active topics (useful for debugging)
        public string[] GetActiveTopics()
        {
            var topics = new string[_observersByTopic.Count];
            _observersByTopic.Keys.CopyTo(topics, 0);
            return topics;
        }
        
        // Static wrapper methods for additional functionality
        public static bool HasObserversStatic(string topicName)
        {
            return Instance.HasObservers(topicName);
        }
        
        public static int GetObserverCountStatic(string topicName)
        {
            return Instance.GetObserverCount(topicName);
        }
        
        public static void ClearTopicStatic(string topicName)
        {
            Instance.ClearTopic(topicName);
        }
        
        public static void ClearAllStatic()
        {
            Instance.ClearAll();
        }
        
        public static string[] GetActiveTopicsStatic()
        {
            return Instance.GetActiveTopics();
        }
        
        // Cleanup when object is destroyed
        private void OnDestroy()
        {
            if (_instance == this)
            {
                ClearAll();
                _instance = null;
            }
        }
    }
}