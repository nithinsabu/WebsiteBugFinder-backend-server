This is the backend server of the Website Bug Finder project. 
Dotnet version 8.0

To run the project, ensure the following environment variables:
"WebpageAnalyseDatabase": {
      "ConnectionString": "mongodb://localhost:27017",
      "DatabaseName": "WebpageAnalyse",
      "WebpagesCollectionName": "Webpages",
      "UsersCollectionName": "Users",
      "WebpageAnalysisResultsCollectionName": "WebpageAnalysisResults"
    }
This is mentioned in appsettings.json

Additional environmental variables required:
| Variable Name                                    | Description                         |
|--------------------------------------------------|-------------------------------------|
| `PYTHONSERVER__CONNECTIONSTRING`                 | URL of the FastAPI server           |
| `AXECORE__CONNECTIONSTRING`                      | URL of the AxeCore service          |
| `PAGESPEEDAPI__CONNECTIONSTRING`                 | Google PageSpeed API endpoint       |
| `PAGESPEEDAPI__API_KEY`                          | API key for PageSpeed               |
| `NUVALIDATOR__CONNECTIONSTRING`                  | URL of W3C Nu Validator             |

Mention these in appsettings.json or appsettings.{Environment}.json or set manually. Example: mention the PYTHONSERVER__CONNECTIONSTRING in configuration json as: {PythonServer: {ConnectionString: "<Python server connection string>"}}

Run it locally by:
1. cd to WBF: cd 
2. Build the project: dotnet build
3. Run the project: dotnet run
(Runs on port 5254)

Run the docker image by:
1. docker build -t backend-app .
2. docker run -e <Provide all environment variables mentioned above> --name <Provide name> --network <Provide network> -p <PORT>:8080 backend-app
(Runs on port 8080 of docker container)