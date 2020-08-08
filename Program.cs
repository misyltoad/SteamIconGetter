using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using SteamKit2;

namespace SteamIconGetter
{
    class Program
    {
        static SteamApps       m_steamApps;
        static SteamUser       m_steamUser;
        static SteamClient     m_steamClient;
        static CallbackManager m_manager;
        static HttpClient      m_http;

        static bool m_isRunning; 

        public static void Main( string[] args )
        {
            m_http        = new HttpClient();
            m_steamClient = new SteamClient();
            m_manager     = new CallbackManager( m_steamClient );

            m_steamUser = m_steamClient.GetHandler<SteamUser>();
            m_steamApps = m_steamClient.GetHandler<SteamApps>();

            m_manager.Subscribe<SteamClient.ConnectedCallback>   ( OnConnected );
            m_manager.Subscribe<SteamClient.DisconnectedCallback>( OnDisconnected );
            m_manager.Subscribe<SteamUser.LoggedOnCallback>      ( OnLoggedOn );
            m_manager.Subscribe<SteamUser.LoggedOffCallback>     ( OnLoggedOff );

            m_isRunning = true;

            Console.WriteLine( "Connecting to Steam..." );

            m_steamClient.Connect();

            while ( m_isRunning )
            {
                m_manager.RunWaitCallbacks( TimeSpan.FromSeconds( 1 ) );
            }
        }

        static void OnConnected( SteamClient.ConnectedCallback callback )
        {
            m_steamUser.LogOnAnonymous();
        }

        static void OnDisconnected( SteamClient.DisconnectedCallback callback )
        {
            Console.WriteLine( "Disconnected from Steam" );

            m_isRunning = false;
        }

        static async void OnLoggedOn( SteamUser.LoggedOnCallback callback )
        {
            if ( callback.Result != EResult.OK )
            {
                Console.WriteLine( "Unable to logon to Steam: {0} / {1}", callback.Result, callback.ExtendedResult );

                m_isRunning = false;
                return;
            }

            Console.WriteLine( "Successfully logged on!" );

            Directory.CreateDirectory( "icons" );
            for (uint i = 0; i < 200000; i++)
            {
                // appids come in multiples of 10
                uint appid = i * 10;
                if ( File.Exists( $"icons/{appid}.ico" ) )
                {
                    Console.WriteLine( $"Already have icon for {appid}" );
                    continue;
                }

                if ( File.Exists( $"icons/{appid}.noicon" ) )
                {
                    Console.WriteLine( $"We know {appid} has no appid" );
                    continue;
                }

                Console.WriteLine( $"Looking up clienticon for {appid}" );
                try
                {
                    var productInfo = await m_steamApps.PICSGetProductInfo( appid, null );
                    var hash = productInfo.Results[ 0 ].Apps[ appid ].KeyValues[ "common" ][ "clienticon" ].Value;
                    if ( string.IsNullOrWhiteSpace( hash ) )
                    {
                        File.Create( $"icons/{appid}.noicon" );
                        continue;
                    }

                    Console.WriteLine( $"Got hash {hash}" );
                    var resp = await m_http.GetAsync( $"http://media.steampowered.com/steamcommunity/public/images/apps/{appid}/{hash}.ico?original=1" );
                    using ( var fs = new FileStream( $"icons/{appid}.ico", FileMode.OpenOrCreate ) )
                        await resp.Content.CopyToAsync( fs );
                }
                catch (Exception e)
                {
                    File.Create( $"icons/{appid}.noicon" );
                }
            }

            m_steamUser.LogOff();
        }

        static void OnLoggedOff( SteamUser.LoggedOffCallback callback )
        {
            Console.WriteLine( "Logged off of Steam: {0}", callback.Result );
        }
    }
}
