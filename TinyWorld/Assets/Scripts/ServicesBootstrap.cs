using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

namespace TinyWorld
{
    public static class ServicesBootstrap
    {
        public static Task Initialization { get; private set; }
        public static bool IsOnline { get; private set; }
        public static string LastError { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            Initialization = null;
            IsOnline = false;
            LastError = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Begin()
        {
            Initialization = ConnectAsync();
        }

        static async Task ConnectAsync()
        {
            try
            {
                await UnityServices.InitializeAsync();
                if (!AuthenticationService.Instance.IsSignedIn)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                IsOnline = AuthenticationService.Instance.IsAuthorized;
                Debug.Log("Anonymous player identity ready.");
            }
            catch (Exception error)
            {
                LastError = error.GetType().Name;
                Debug.LogWarning($"Online services unavailable; continuing offline ({LastError}).");
            }
        }
    }
}
