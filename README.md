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
| `WEBPAGEANALYSEDATABASE__CONNECTIONSTRING`       | URL of the mongoDB database         |
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

Example run:
docker run --name backend-app --network wbfapp -e PYTHONSERVER__CONNECTIONSTRING=http://fastapi-app:80 -e AXECORE__CONNECTIONSTRING=http://host.docker.internal:4000 -e PAGESPEEDAPI__CONNECTIONSTRING=https://www.googleapis.com/pagespeedonline/v5/runPagespeed -e PAGESPEEDAPI__API_KEY=AIzaSyDRd8-ZNhRLb6GHF8RJRmDNCMAZdUU2GjA -e NUVALIDATOR__CONNECTIONSTRING=https://validator.w3.org/nu/ -e WEBPAGEANALYSEDATABASE__CONNECTIONSTRING=mongodb://mongodb:27017 -p 5254:8080 backend-app:latest 

(Expose 5254 to host)