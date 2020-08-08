using System;
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
        static uint            m_appid;

        static bool m_isRunning; 

        public static void Main( string[] args )
        {
            if ( args.Length != 1 )
            {
                Console.WriteLine( "Please specify an appid to get the icon of." );
                return;
            }

            m_appid       = uint.Parse( args[0] );
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

            Console.WriteLine( $"Looking up clienticon for {m_appid}" );
            var productInfo = await m_steamApps.PICSGetProductInfo( m_appid, null );
            var hash = productInfo.Results[ 0 ].Apps[ m_appid ].KeyValues[ "common" ][ "clienticon" ].Value;

            Console.WriteLine( $"Got hash ${hash}" );
            var resp = await m_http.GetAsync( $"http://media.steampowered.com/steamcommunity/public/images/apps/{m_appid}/{hash}.ico?original=1" );
            using ( var fs = new FileStream( $"{m_appid}.ico", FileMode.OpenOrCreate ) )
                await resp.Content.CopyToAsync( fs );

            m_steamUser.LogOff();
        }

        static void OnLoggedOff( SteamUser.LoggedOffCallback callback )
        {
            Console.WriteLine( "Logged off of Steam: {0}", callback.Result );
        }
    }
}
