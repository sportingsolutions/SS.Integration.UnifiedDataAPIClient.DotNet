This is the master repository for the Sporting Solutions Unified Data API Client for the .Net Framework.

Getting Started
----------------------
	
	ICredentials credentials = new Credentials { UserName = "jim@bookies", Password = "password" };
	var theSession = SessionFactory.CreateSession(new Uri("http://api.sportingsolutions.com"), credentials);
	
	//Get the Unified Data API Service
	var theService = theSession.GetService("UnifiedDataAPI");
	
	//Sports are features, so lets get Tennis
	var theFeature = theService.GetFeature("Tennis");
	
	//Events are resources, lets get all the events for Tennis
	var theResources = theFeature.GetResources();
	
	//Grab the first event, this is only an example after all
	var theEvent = theResources.First();
  
	var theSnapshot = theEvent.GetSnapshot();
	System.Console.WriteLine(theSnapshot);

	//Set up the Stream Event handlers
	theEvent.StreamConnected += (sender, args) => System.Console.WriteLine("Stream Connected");
	theEvent.StreamEvent += (sender, args) => System.Console.WriteLine(args.Update);
	theEvent.StreamDisconnected += (sender, args) => System.Console.WriteLine("Stream Disconnected");
 
	theEvent.StartStreaming();
 
It really is as easy as that!