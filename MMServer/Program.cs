#define buildtest


using System;
using WebSocketSharp;
using WebSocketSharp.Server;

using System.Collections.Generic; //lists
using System.Linq; //dictionary keys toarray
using System.Collections; //enumerator
using System.Linq.Expressions;
using MongoDB.Bson.Serialization.Attributes;
using System.Threading;

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
//mongodb apparently has an issue deserializing structs, otherwise I might use that

public class listing
{
    //names are truncated to save on significant bandwidth to mongodb server
    [BsonIgnoreIfDefault]
    public ObjectId _id; //id in mongodb
    public DateTime d; //date
    public char t, M; //type, map
    public string m, p; //max, players
    public string I, P, n, N, D; //ip, port, playername, gamename, description

    public string u; //uuid

    public listing(DateTime _date, char _type, char _map, string _max, string _players, string _ip, string _port, string _playername, string _gamename, string _description, string uuid)
    {
        d = _date; t = _type; M = _map; m = _max; p = _players; I = _ip; P = _port; n = _playername; N = _gamename; D = _description;
        //_id = MongoDB.Bson.ObjectId.GenerateNewId(DateTime.Now);
        u = uuid; //UUID of client
    }
    public listing(DateTime _date, char _type, char _map, string _max, string _players, string _ip, string _port, string _playername, string _gamename, string _description, string uuid, ObjectId _id)
    {
        d = _date; t = _type; M = _map; m = _max; p = _players; I = _ip; P = _port; n = _playername; N = _gamename; D = _description;
        //this._id = _id;
        u = uuid; //UUID of client
    }

    public override string ToString()
    {

        return t.ToString() + M.ToString() + p.ToString() + m.ToString() + I + delimeters.slisting + P + delimeters.slisting + n + delimeters.slisting + N + delimeters.slisting + D + delimeters.slisting + u;
    }

    //empty list reference
    public static readonly listing empty = new listing(default(DateTime), default(char), default(char), null, null, null, null, null, null, null, null);

    //used for testing against null in some cases where we need to check to see if we can resolve a property name
    public static readonly listing dummy = new listing(DateTime.Now, 'T', 'T', "1", "T", "127.0.0.1", "41389", "dummy", "dummygame", "description", "dummyID");
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

    public static char[] listingGroups = { '±' };
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

        const int idLength = 16; //16 bytes for UUID....

        public static MongoClient mongoClient;
        public static IMongoDatabase main;
        public static IMongoCollection<listing> games;

        static FindOneAndReplaceOptions<listing> updateOptions;

        static bool mongoSetup;
        public static void SetupMongo(string connectionString, string dbName)
        {
            if (mongoSetup)
                return;
            mongoSetup = true; //prevent second init

            // Create a MongoClient object by using the connection string
            mongoClient = new MongoClient(connectionString);

            // Use the client to access the 'test' database
            main = mongoClient.GetDatabase(dbName);
            games = main.GetCollection<listing>("g");
            updateOptions = new FindOneAndReplaceOptions<listing>();
            updateOptions.IsUpsert = true;
            updateOptions.ReturnDocument = ReturnDocument.After;

        }

        protected override void OnMessage(MessageEventArgs e)
        {
            try {
                Console.WriteLine("received data");
                Console.WriteLine("received from " + Context.UserEndPoint.Address.ToString() + ":" + Context.UserEndPoint.Port.ToString() + " with data: " + e.Data + "\n binary: " + BitConverter.ToString(e.RawData));

                switch (e.Data[0])
                {
                    case mmcodes.get:
                        HandleGet(e.Data.Substring(0, e.Data.Length - idLength), e.Data.Substring(e.Data.Length - idLength, idLength)); //separate DATA and ID
                        break;
                    case mmcodes.create:
                        HandleCreate(e.Data.Substring(0, e.Data.Length - idLength), e.Data.Substring(e.Data.Length - idLength, idLength)); //separate DATA and ID
                        break;
                }
            } catch (ObjectDisposedException ex) {
                Console.WriteLine("caught error: \n" + ex);
            }


} //end


        //used for sorting games...
        readonly SortDefinitionBuilder<listing> sort = new SortDefinitionBuilder<listing>();

        async void HandleGet(string data, string id)
        {

            if (data.Length < 7)
            {
                Console.WriteLine("get input was too short at" + data.Length);
                return;
            }

            var builder = Builders<listing>.Filter;


            string thenby = sortBy.players.ToString(); //set second sort by initially to sort secondarily to current num of players...
            //if we're actually requested to sort by that already, sort secondarily by max players instead
            if (data[6] == sortBy.players)
            {
                thenby = sortBy.max.ToString();
            }

            //sanity check input of requested sort type to avoid some null reference exceptions
            if (listing.dummy.GetType().GetField(data[6].ToString()) == null || listing.dummy.GetType().GetField(thenby) == null)
            {
                //invalid sort request input from user
                //throw new ArgumentOutOfRangeException();

                Console.WriteLine("invalid sort type requested: " + data[6].ToString());
                return;
            }

            List<listing> membersList =
             await
                 games.Find(new BsonDocument())
                     .Sort(sort.Descending(data[6].ToString()))
                     .Sort(sort.Descending(thenby))
                     .Limit(Program.numReturns)
                     .ToListAsync();



            Console.WriteLine("found " + membersList.Count + " games");

            Send(
                mmcodes.get.ToString() + String.Join(delimeters.slistingGroups, membersList) //send each listing separated by delimeter, each listing being converted to string
                );

        } //end HandleGet

        async void HandleCreate(string data, string uuid)
        {
            var builder = Builders<listing>.Filter;

            string[] split = data.Split(delimeters.create); //split by delimeter

            if (split[0].Length < 10)
            {
                Console.WriteLine("create input length was too short at " + split[0].Length);
                return;
            }

            //filter is where it is the same IP and port num
            //EDIT: now UUID sent by client in the last 16 bytes... because otherwise websocket clients can't be distinguished by IP + PORT!
            var filter =
                builder.Eq(
                    //Builders<listing>.Filter.Eq("I", Context.UserEndPoint.Address.ToString()),
                    //Builders<listing>.Filter.Eq("P", data.Substring(9, data.IndexOf(delimeters.create) - 9))
                    listing.dummy.u, uuid
                    );


            //upsert using filter
            var results = await games.FindOneAndReplaceAsync<listing>(
                doc => doc.u == uuid,
                new listing(DateTime.Now, data[1], data[2], data.Substring(3, 3), data.Substring(6, 3), Context.UserEndPoint.Address.ToString(), data.Substring(9, data.IndexOf(delimeters.create) - 9), split[1], split[2], split[3], uuid),
                updateOptions
            );

            Console.WriteLine("replace results: " + results);
        }

        async void HandleRemove(string data, string uuid)
        {
            var builder = Builders<listing>.Filter;

            //filter is where it is the same IP and port num
            //EDIT: now UUID
            var filter =
                builder.Eq(
                    //Builders<listing>.Filter.Eq("I", Context.UserEndPoint.Address.ToString()),
                    //Builders<listing>.Filter.Eq("P", data.Substring(1, data.Length - 1))
                    listing.dummy.u, uuid
                    );


            var results = await games.DeleteOneAsync(
                filter
                );

            Console.WriteLine("deleted " + results.DeletedCount);
        }

    } // end class server

    public class Program
    {


        const int gameTimeoutSecs = 600;
        static int port = 9002; //host port

        const string connectionString = "mongodb://heroku_ : .mlab.com: /";
        const string dbName = "heroku_";

        public static int numReturns = 10; //number of game listings to return on a request


        public static void Main(string[] args)
        {
            MMServer.SetupMongo(connectionString, dbName); //init

            int listenPort;
            if (!int.TryParse(Environment.GetEnvironmentVariable("PORT"), out listenPort))
                listenPort = port;

            Console.WriteLine("...listening on port " + listenPort + "...");

            var wssv = new HttpServer(listenPort, false);
            
            
            wssv.AddWebSocketService<MMServer>(
                "/MM"
            );


            wssv.KeepClean = false;

                wssv.Start();
                //Console.ReadKey(true);
                Console.ReadLine ();
                while(true)
                {
                    Thread.Sleep(2);
                }

#endif
                wssv.Stop();
        } //end Main



    } //end class Program

} //end namespace
