/*! \mainpage GTP SDK 

This is the master repository for the Sporting Solutions Unified Data API Client for the .Net Framework.
The SDK provides an easy to use interface into the Sporting Solutions Unified Data API.  

Usage of this SDK requires a GTP username and password (available on request), it's usage is authorised only for current or prospective clients.

Any bug reports, comments, feedback or enhancements requests are gratefully received.

Dependencies
----------------------
You will need [Microsoft .NET Framework 4.0](http://www.microsoft.com/download/en/details.aspx?id=17718) to compile and use the library on Windows

Licence
----------------------
Sporting Solutions Unified Data API Client for the .Net Framework is licenced under the terms of the Apache Licence Version 2.0, please see the included Licence.txt file

Getting Started
----------------------
```c#	
ICredentials credentials = new Credentials { UserName = "jim@bookies", Password = "password" };
var theSession = SessionFactory.CreateSession(new Uri("http://{url here}"), credentials);

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
```

