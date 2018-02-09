using MEC;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;
using WebSocketSharp;
using WebSocketSharp.Net;

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




public class MMClient : MonoBehaviour {

    #region singleton
    //singleton code
    // s_Instance is used to cache the instance found in the scene so we don't have to look it up every time.
    private static MMClient s_Instance = null;
    //Singleton code
    // s_Instance is used to cache the instance foull;
    // This defines a static instance property that attempts to find the manager ob ect in the scene and
    // returns it to the caller.
    public static MMClient instance
    {
        get
        {
            if (s_Instance == null)
            {
                // This is where the magic happens.
                //  FindObjectOfType(...) returns the first AManager object in the scene.
                s_Instance = FindObjectOfType(typeof(MMClient)) as MMClient;
            }

            // If it is still null, create a new instance
            if (s_Instance == null)
            {
                GameObject obj = new GameObject("MMClient");
                s_Instance = obj.AddComponent(typeof(MMClient)) as MMClient;
                Debug.Log("Could not locate an MMClient object. MMClient was Generated Automaticly.");
            }
            return s_Instance;
        }
    }
    #endregion

    public float fetchGamesInterval = 20;

    public delegate void OnReceiveGames(Dictionary<string, listing> listings);
    public static OnReceiveGames onReceiveGames;



    static WebSocket ws;
    public string serverAddress = "localhost:9002"; //example

    List<MessageEventArgs> messageQueue = new List<MessageEventArgs>(); //used so we can technically call things from the main thread like Instantiate... not really important to do it faster than the next frame because this is only matchmaking.


    BackgroundWorker worker;

    public void Start()
    {
        // worker.DoWork += new DoWorkEventHandler(backgroundWorker1_DoWork);

        StartMM(serverAddress);

    }

    private void Update()
    {
        //see messageQueue above
        while(messageQueue.Count > 0)
        {
            HandleMessage(messageQueue[0]);
            messageQueue.RemoveAt(0);
        }
    }

    bool initialized = false;
    public void StartMM(string server)
    {
        if (ws != null && ws.IsAlive)
            return;

        ws = new WebSocket("ws://" + server);
        // Set the WebSocket events.


        ws.OnOpen += (sender, e) => HandleOpen(sender, ref e);

        ws.OnMessage += (sender, e) => QueueMessage(sender, ref e);

        ws.OnError += (sender, e) => HandleError(sender, ref e);

        ws.OnClose += (sender, e) => HandleClose(sender, ref e);

        initialized = true;
        ws.Connect();

    }

    Coroutine fetchCoroutine;
    public void StopMM()
    {
        ws.Close();
        if (fetchCoroutine != null)
            StopCoroutine(fetchCoroutine);
        HostStopUpdating();
    }

    void HandleOpen(object sender, ref EventArgs e)
    {
        Debug.Log("connected to matchmaking");
        if (ws != null && ws.IsAlive)
            fetchCoroutine = Ext.RepeatAction(fetchGamesInterval, () => FetchGames(), true);
    }

    void QueueMessage(object sender, ref MessageEventArgs e)
    {
        messageQueue.Add(e);
    }

    void HandleMessage(MessageEventArgs e)
    {

        if (e.Data.Length < 1)
        {
            Debug.Log("received empty message");
            return; //return for empty messages...
        }

        //Debug.Log("received " + e.Data);

        switch (e.Data[0])
        {
            case mmcodes.get:
                ReceiveGames(e.Data);
                break;

        }
    }

    public void HostStopUpdating()
    {
        if (refreshListing != default(CoroutineHandle))
            Timing.KillCoroutines(refreshListing);
        refreshListing = default(CoroutineHandle);
    }

    static bool reconnecting = false;
    public static float tryReconInterval = 5f;
    static IEnumerator Reconnect()
    {
        while(ws == null || !ws.IsAlive && reconnecting)
        {
            MMClient.instance.StartMM(MMClient.instance.serverAddress);
            yield return new WaitForSeconds(tryReconInterval);
        }
        reconnecting = false;
    }

    static void HandleError(object sender, ref ErrorEventArgs e)
    {
        if (ws == null || !ws.IsAlive && !reconnecting)
        {
            reconnecting = true;
            MMClient.instance.StartCoroutine(Reconnect());
        }


        Debug.LogError("error event on MM: \n" + e.Message + "\n\n" + e.Exception);
    }
    static void HandleClose (object sender, ref CloseEventArgs e)
    {
        Debug.Log("disconnected from matchmaking");
    }


    public void SetFilter()
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// sets the filter from variables inserted, mostly for debug. To merely fetch them from the specified values in the GUI, use SetFilter();
    /// </summary>
    public void SetFilterRaw(char gameType, char gameMap, int maxPlayers, char sortBy)
    {
        wantedGameType = gameType;
        wantedGameMap = gameMap;
        wantedMaxPlayers = maxPlayers;
        wantedSortType = sortBy;
    }

    //these three are to be set to what you want the MM to find from MM. Set from beginning to wildcards to get all games as default
    char wantedGameType = delimeters.wildcard;
    char wantedGameMap = delimeters.wildcard;
    int wantedMaxPlayers = int.Parse(delimeters.numwildcard);
    char wantedSortType = sortBy.players; //default sort by num playerss
    CoroutineHandle refreshListing;
    public float refreshInterval = 4 * 60; //how often to ping MM server with our listing...

    public void FetchGames()
    {
        ws.Send(
            mmcodes.get.ToString() + wantedGameType.ToString() + wantedGameMap.ToString() + Ext.PadInt(wantedMaxPlayers, 3) + wantedSortType.ToString() + NetworkInterface.serverGUID //send what we specified. playermax is padded to always be 3 characters
        );
    }
    
    /*
    public void CreateGame(char gameType, char gameMap, int playersMax, int startPlayers, string port, string playerName, string gameName, string description)
    {
        string sends = mmcodes.create.ToString() + gameType.ToString() + gameMap.ToString() + Ext.PadInt(playersMax, 3) + Ext.PadInt(startPlayers, 3) + 
            port
            //NetworkInterface.serverGUID
            + delimeters.screate + playerName + delimeters.screate + gameName + delimeters.screate + description;
        if (ws != null)
            ws.Send(sends + id);
        Debug.Log("sending " + sends);

        if (refreshListing != default(CoroutineHandle))
            Timing.KillCoroutines(refreshListing);
        refreshListing = Timing.CallContinuously(refreshInterval, ()=> { ws.Send(sends + id); } );


    }*/

    public void CreateGame(listing l)
    {
        hostedGame = l;
        SerializeHostedGame(); //serializes it too

        Debug.Log("sending " + serializedGame);

        if (refreshListing != default(CoroutineHandle))
            Timing.KillCoroutines(refreshListing);
        refreshListing = Timing.RunCoroutine(RefreshGame());

    }

    public listing hostedGame;
    string serializedGame;



    IEnumerator<float> RefreshGame()
    {
        while (ws != null) {
            ws.Send(serializedGame + NetworkInterface.serverGUID);
            yield return Timing.WaitForSeconds(refreshInterval);
        }
    }

    public void RefreshGameNow()
    {
        if (ws != null)
        {
            ws.Send(serializedGame + NetworkInterface.serverGUID);
        }
        Debug.Log("sent hosted game to server");
    }

    //update the serialized version that's being sent to server
    public void SerializeHostedGame()
    {
        serializedGame = mmcodes.create.ToString() + hostedGame.type.ToString() + hostedGame.map.ToString() + Ext.PadInt(int.Parse(hostedGame.max), 3) + Ext.PadInt(int.Parse(hostedGame.players), 3) +
            hostedGame.port
            //NetworkInterface.serverGUID
            + delimeters.screate + hostedGame.playername + delimeters.screate + hostedGame.gamename + delimeters.screate + hostedGame.description;
    }

    public static Dictionary<string, listing> listings = new Dictionary<string, listing>();
    public void ReceiveGames(string data)
    {
        //create list of listings
        //Debug.Log("receivedGames: \n" + data);

        string[] split = data.Substring(1, data.Length - 1).Split(delimeters.listingGroups, StringSplitOptions.RemoveEmptyEntries); //remove mmcode and divide into individual listings
        string[] fields; //individual listings data but split. temp variable
        for(int i = 0; i < split.Length; i++)
        {
           // Debug.Log("parsing " + i  + "/" + (split.Length) + ": " + split[i]);

            fields = new string(split[i].ToCharArray()).Split(delimeters.listing); //copy to temp to avoid splitting orig and split listing into readable fields

           // Debug.Log("fields: " + fields.Length);

          //  Debug.Log("game running on " + split[i].Substring(8, split[i].IndexOf(delimeters.slisting) - 8) + ":" + fields[1] + " at " + fields[5]);

            if (!listings.ContainsKey(fields[5])) { //if dict doesn't contain listing for ip+port given, add, otherwise replace
                //listings.Add(split[i].Substring(8, split[i].IndexOf(delimeters.slisting) - 8) + fields[1],
                listings.Add(fields[5],       //actually guid now...
                    new listing(
                    DateTime.Now, //date
                    fields[0][0], //game type
                    fields[0][1], //map
                    fields[0].Substring(2, 3), //num max
                    fields[0].Substring(5, 3), //num connected
                    split[i].Substring(8, split[i].IndexOf(delimeters.slisting) - 8), //ip is from char 9 to the first delimeter
                    fields[1], //ports
                    fields[2], //game host playername
                    fields[3], //game name
                    fields[4], //description
                    fields[5] //guid
                    ));
            } else
            { //replace
                //listings[split[i].Substring(8, split[i].IndexOf(delimeters.slisting) - 8) + fields[1]] =
                listings[fields[5]] =       //actually guid now...
                    new listing(
                    DateTime.Now, //date
                    fields[0][0], //game type
                    fields[0][1], //map
                    fields[0].Substring(2, 3), //num max
                    fields[0].Substring(5, 3), //num connected
                    split[i].Substring(8, split[i].IndexOf(delimeters.slisting) - 8), //ip is from char 9 to the first delimeter
                    fields[1], //port
                    fields[2], //game host playername
                    fields[3], //game name
                    fields[4], //description
                    fields[5]
                    );
            }
        }



        if (onReceiveGames != null && listings != null)
            onReceiveGames(new Dictionary<string, listing>(listings)); //pass copy to avoid read/write errors to OfflineGUI
    }

    
    
    /*
    -----------
    Example of how to handle the above code
    -----------
    
    Dictionary<string, RectTransform> listingObjMap = new Dictionary<string, RectTransform>();

    //from MMClient.onReceiveGames
    private void ReceiveGames(Dictionary<string, listing> listings)
    {
        //for reference-     public listing(DateTime _date, char _type, char _map, int _max, int _players, string _ip, string _port, string _playername, string _gamename, string _description)

        //list games


        //remove old
        foreach(KeyValuePair<string,RectTransform> entry in listingObjMap)
        {
            if(!listings.ContainsKey(entry.Key)) //if updated list in listings doesn't have... remove it and it's object
            {
                GameObject.Destroy(entry.Value.gameObject); //delete listings obj
                listingObjMap.Remove(entry.Key); //remove from map
            }
        }
        

        int i = 0;

        foreach(KeyValuePair<string, listing> entry in listings)
        {
            RectTransform temp;
            if(listingObjMap.ContainsKey(entry.Key))
            {
                temp = listingObjMap[entry.Key]; //working with already created... merely overwrite values
            } else
            {
                //doesn't exist, create...
                temp = ((GameObject)(Ext.Instantiate(listingObject, listingContentArea))).GetComponent<RectTransform>();

                if (oneClickJoinListing)
                    temp.GetChild(8).GetComponent<Button>().onClick.AddListener(delegate { JoinGame(entry.Value); }); //add button function
                else
                    temp.GetChild(8).GetComponent<Button>().onClick.AddListener(delegate { SetSelectedGame(entry.Value, temp); }); //add button function

                listingObjMap.Add(entry.Key, temp); //add to reference

            }

            temp.GetChild(0).GetComponent<Text>().text = entry.Value.type.ToString(); //set 
            temp.GetChild(1).GetComponent<Text>().text = entry.Value.map.ToString(); //set
            temp.GetChild(2).GetComponent<Text>().text = entry.Value.max.ToString(); //set 
            temp.GetChild(3).GetComponent<Text>().text = entry.Value.players.ToString(); //set 
            temp.GetChild(4).GetComponent<Text>().text = entry.Value.ip; //set 
            temp.GetChild(5).GetComponent<Text>().text = entry.Value.port; //set 
            temp.GetChild(6).GetComponent<Text>().text = entry.Value.playername; //set 
            temp.GetChild(7).GetComponent<Text>().text = entry.Value.gamename; //set 
            temp.GetChild(7).GetComponent<Text>().text = entry.Value.description; //set 

            if (i % 2 == 0) //every other one
                temp.GetChild(8).GetComponent<Image>().color = stripeColor; //make it striped
            else
                temp.GetChild(8).GetComponent<Image>().color = normalListingColor; // make it normal

            temp.anchoredPosition = listingObject.GetComponent<RectTransform>().anchoredPosition; //reset for lazy calcs...

            temp.position += new Vector3(0, i * (-listingObject.GetComponent<RectTransform>().rect.height - listingEntryPadding), 0);
            temp.gameObject.SetActive(true);




            i++;
        }
        


        listingContentArea.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, listings.Count * (listingObject.GetComponent<RectTransform>().rect.height + listingEntryPadding));

    }
    */
    
    


} //end class
