//#define buildtest


//this should run from the console. There is a preprocessor directive set to make Console.ReadKey uncommented to make this work. It is commented for standard build types because it causes an error to these builds.


using System;
using WebSocketSharp;
using WebSocketSharp.Server;

using System.Collections.Generic; //lists
using System.Linq; //dictionary keys toarray
using System.Collections; //enumerator

#if buildtest
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

/*
public struct fetchGamesPacket {
    public const char code = mmcodes.get; //message type identifier byte
    public string wantedGameType;
    public string wantedGameMap;
    public string wantedMaxPlayers;

    public fetchGamesPacket(string _wantedGameType, string _wantedGameMap, int _wantedMaxPlayers)
    {
        wantedGameType = _wantedGameType;
        wantedGameMap = _wantedGameMap;
        wantedMaxPlayers = _wantedMaxPlayers.ToString("000"); 
    }
}*/




#endif
//an element which represents a hosted game
#if test
public struct listing
{
    public DateTime date;
    public char type, map;
    public string max, players;
    public string ip, port, playername, gamename, description;
    public string uuid;

    public listing(DateTime _date, char _type, char _map, string _max, string _players, string _ip, string _port, string _playername, string _gamename, string _description, string _uuid)
    {
        date = _date; type = _type; map = _map; max = _max; players = _players; ip = _ip; port = _port; playername = _playername; gamename = _gamename; description = _description; uuid = _uuid;
    }

    public override string ToString()
    {
        return type.ToString() + map.ToString() + players.ToString() + max.ToString() + ip + delimeters.slisting + port + delimeters.slisting + playername + delimeters.slisting + gamename + delimeters.slisting + description + delimeters.slisting + uuid;
    }
}
public struct mmcodes
{
    public const char
        get = 'm',
        create = 'c'
    ;
}

public struct delimeters
{
    //char array is used to string.Split(delimeters.listing) on client...
    //string is used to build string to send separted by these delimeters from server
    //listing is what splits all the elements of the listing
    //listingGroups is what separates each of these listings from each other

    public static char[] listing = { '¨' };
    public static string slisting = "¨"; //string version... messy

    public static char[] listingGroups = {'±'};
    public static string slistingGroups = "±";//string version... messy



    public const char
        wildcard = '*',
        create = '~'
    ;

    public static string
        swildcard = "*",
        screate = "~"
    ;

    public const string
        numwildcard = "000" //wildcard used on numbers of players for sorting
    ;
}
public struct sortBy
{
    public const char
        date = 'd',
        type = 't',
        map = 'M',
        max = 'm',
        players = 'p',
        playername = 'n',
        gamename = 'N',
        description = 'D'
        ;
}
#endif

#if buildtest

/*  Maybe I have to pass all the information to the Unity instance on the web browser so it can be processed in C# instead of Javascript therefore giving it the ability to handle byte[]. 
 *  But it's mostly text, so I think it's more of an advantage to use strings
 * 
 *  create pagination functions on both client and server to ask and retrieve top / next 10 or so games
 *  add game type filtering to the request and response
 *  add fields like description, game name, map
 *  
 *  make the registering servers continuously ping the server to update ping, number players, etc
 *  to port to web, make registering servers (give the option) register on the handshaking server for WebRTC connections
 *  WebRTC players cannot host a game probably... unless I make the JS middleware
 *  
 *  add ping
 *
 */


namespace WSSServer
{

    public class MMServer : WebSocketBehavior
    {


        int numReturns = 10; //number of game listings to return;

        //listing struct is on MMClient

        //the strategy to avoid read/write errors is to use the below variable to switch reference between the lower two.
        //we copy from A to B, switch the reference of the tool variable to B,  remove the old values from A, then switch it back and add all that were added in the meantime on B to A
        //Dictionary is a class and therefore pass
        //ed by reference, but we need to copy over the values between every switch.
        //key is ip+port
        public static Dictionary<string, listing> listings = new Dictionary<string, listing>();

		public static Dictionary<string, listing> listingsA = new Dictionary<string, listing>();
		public static Dictionary<string, listing> listingsB = new Dictionary<string, listing>(); //use a temp to avoid read/write errors on a dictionary

		protected override void OnMessage (MessageEventArgs e)
		{
			Console.WriteLine ("received from " + Context.UserEndPoint.Address.ToString() + ":" + Context.UserEndPoint.Port.ToString() + " with data: " + e.Data + "\n binary: " + BitConverter.ToString(e.RawData));

            switch (e.Data[0]) {
			    case mmcodes.get:
                    HandleGet(e.Data);
			        break;
			    case mmcodes.create:
                    HandleCreate(e.Data);
				    break;
			}

			/*
				var msg = e.Data == "BALUS"
				? "I've been balused already..."
				: "I'm not available now.";

				Send (msg);
			*/
			
		} //end `



        /* SERVER sent fetch code below. On client create code, there is no asking for specific sorting. The IP is also deduced from server for security. Therefore, it is the same as below except without sending IP and sorting data.
         * 
        * first char is mmcode, second is game type (if any), third is game map (if any), next three chars are number of players max, next three are number of players,
        * then there's a char for the sorting type. Whether this char is uppercase or lowercase determines if it's uppercase or lowercase
        * the possible options are N number players, M max players
        * then there's the ip, port, game host player name, game name, and description which are separated by delimeter
        * 
        */

        void HandleGet(string data)
        {
            if (listings.Count == 0)
                return; //no games

            string returns = mmcodes.get.ToString();

            //TODO: if looping add host to a list of people to notify when it finishes...
            int num = numReturns; //number of listings to return

            foreach(listing l in listings.Values)
            {
                //do checks for gametype filtering
                if (data[1] != delimeters.wildcard && data[1] != l.type) //if data[1] (the game type) isn't wildcard (meaning doesn't matter) AND we don't have the same game type
                    continue; //dont add to returns
                if (data[2] != delimeters.wildcard && data[2] != l.map) //if data[2] (the game map) isn't wildcard (meaning doesn't matter) AND we don't have the same game map
                    continue; //dont add to returns
                if (data.Substring(3, 3) != delimeters.numwildcard && int.Parse(l.max) < int.Parse(data.Substring(3, 3))) //if it matters and we didn't meet minimum players of iterated entry l
                    continue;

                //TODO: sorting by player number / max... would basically have to loop through all and organize them according to player number or cache it (better)
                //lists are passed by reference so this would be good


                returns += l.ToString() + delimeters.slistingGroups; //see listing.ToString()


                num--;
                if (num == 0) //reached end of numReturns
                    break;
            }

            if (returns.Length > 2) //if it's safe to do so, remove the delimeter.slistingGroups on the end so we don't waste bandwidth and get an index error on the client from trying to read the last (blank) entry
                returns = returns.Substring(0, returns.Length - delimeters.slistingGroups.Length);

            Send(returns);

        }


        //code sent to this point by client::   string sends = mmcodes.create.ToString() + gameType.ToString() + gameMap.ToString() + Tools.PadInt(playersMax, 3) + Tools.PadInt(startPlayers, 3) + port + delimeters.create.ToString() + playerName + delimeters.create.ToString() + gameName + delimeters.create.ToString() + description;
        /* SERVER sent fetch code below. On client create code, there is no asking for specific sorting. The IP is also deduced from server for security. Therefore, it is the same as below except without sending IP and sorting data.
         * 
        * first char is mmcode, second is game type (if any), third is game map (if any), next three chars are number of players max, next three are number of players,
        * then there's a char for the sorting type. Whether this char is uppercase or lowercase determines if it's uppercase or lowercase
        * the possible options are N number players, M max players
        * then there's the ip, port, game host player name, game name, and description which are separated by delimeter
        * 
        */
        void HandleCreate(string data)
        {
            Console.WriteLine("received create game: " + data.Substring(1, data.Length - 1));
            string[] split = data.Split(delimeters.create); //split by delimeter
                                                            //add to dictionary where key is ip+port

            //data[1] will read second char... split[1] will read second chunk separated above by delimeter
            //data.Substring(9, data.IndexOf(delimeters.create) - 9) will read from the 9th position to the first delimeter. This should be the port
            //split[0] should be several fields of data with a fixed length which don't need delimeter separation- from the first char (mmcode) to the max players. Then IP is inferred... data.Substring(9, data.IndexOf(delimeters.create) - 9)

            //Console.WriteLine("port: " + data.Substring(9, data.IndexOf(delimeters.create) - 9));

            if (!listings.ContainsKey(Context.UserEndPoint.Address.ToString() + ":" + data.Substring(9, data.IndexOf(delimeters.create) - 9)))
                listings.Add(
                    Context.UserEndPoint.Address.ToString() + ":" + data.Substring(9, data.IndexOf(delimeters.create) - 9), //key
                    new listing(DateTime.Now, data[1], data[2], data.Substring(3, 3), data.Substring(6, 3), Context.UserEndPoint.Address.ToString(), data.Substring(9, data.IndexOf(delimeters.create) - 9), split[1], split[2], split[3]) //value
                    );
            else
                listings[Context.UserEndPoint.Address.ToString() + ":" + data.Substring(9, data.IndexOf(delimeters.create) - 9)] = new listing(DateTime.Now, data[1], data[2], data.Substring(3, 3), data.Substring(6, 3), Context.UserEndPoint.Address.ToString(), data.Substring(9, data.IndexOf(delimeters.create) - 9), split[1], split[2], split[3]); //assignment



           // Console.WriteLine("new game: " + listings[Context.UserEndPoint.Address.ToString() + ":" + data.Substring(9, data.IndexOf(delimeters.create) - 9)].ToString());
        }

        void HandleRemove(string data)
        {
            if(listings.ContainsKey(data.Substring(1, data.Length - 1))) {
                listings.Remove(data.Substring(1, data.Length - 1));
            }
        }

    } // end class server

	public class Program
	{
		
		const int gameTimeoutSecs = 600;
		static ushort port = 9002;

		public static void Main (string[] args)
		{

            MMServer.listings = MMServer.listingsA; //assign reference pointer

            //start timer to check outdated listings
			var timer = new System.Threading.Timer((e) =>
				{
					CheckOutdated();   
				}, null, TimeSpan.Zero, TimeSpan.FromMinutes(1));
			


			Console.WriteLine ("...listening on port " + port + "...");

			var wssv = new WebSocketServer (port);

			wssv.AddWebSocketService<MMServer> (
				"/",
				() =>
				new MMServer () {
					// To ignore the extensions requested from a client.
					IgnoreExtensions = true
				}
			);


			wssv.Start ();
#if !UNITY_EDITOR && !UNITY_WEBGL && !UNITY_STANDALONE_WIN && !UNITY_STANDALONE_OSX && !UNITY_STANDALONE_LINUX
			Console.ReadKey (true);
#endif
            wssv.Stop ();
		} //end Main


        //check if games have expired from their x minute listing time
        static void CheckOutdated() {
            //basically we change the reference to a temp, modify the original, then do a staged copy of the new ones so we don't miss anything 

            if (MMServer.listings.Count < 1)
                return; //no listings

			Console.WriteLine ("checking " + MMServer.listings.Count + " dates...");
            
            MMServer.listingsB = new Dictionary<string, listing>(MMServer.listingsA); //copy by value
            MMServer.listings = MMServer.listingsB; //change reference

            //OPTIMIZE: Cache DateTime.Now?
            foreach (KeyValuePair<string, listing> l in MMServer.listingsA) { //iterate over A
                if (DateTime.Now.Subtract(l.Value.date).TotalSeconds >= gameTimeoutSecs) //if outdated
                    MMServer.listingsA.Remove(l.Key); //remove
            }


            MMServer.listings = MMServer.listingsA; //change back to one we modified

            //add back new entries since time processing above
            foreach (KeyValuePair<string, listing> l in MMServer.listingsB)
            { //iterate over B
                if (!MMServer.listings.ContainsKey(l.Key) && DateTime.Now.Subtract(l.Value.date).TotalSeconds >= gameTimeoutSecs) //if not exists in A and it isn't outdated, add
                    MMServer.listings.Add(l.Key, l.Value);
                //we have to check if the keys are outdated on both because we simply can't modify one of them. Secondly, it could have gotten refreshed by the player on the time during the processing time
            }


        } //end CheckOutdated



	} //end class Program

} //end namespace

#endif

