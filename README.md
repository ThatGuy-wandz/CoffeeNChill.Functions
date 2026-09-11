\# CoffeeNChill Canteen Management System — Part 1



\## Local Setup

\[steps to run locally with func start]



\## Docker — Standalone Execution (No Compose)



\### Prerequisites

\- A real Azure Storage Account connection string for StaffDocsStorage



\### 1. Create the network

docker network create coffeenchill-net



\### 2. Run Azurite

docker run -d --name azurite --network coffeenchill-net -p 10000:10000 -p 10001:10001 -p 10002:10002 mcr.microsoft.com/azure-storage/azurite



\### 3. Build the Functions image

docker build -t modestsnake/coffeenchill-functions:v1.0 .



\### 4. Run the Functions container

docker run -d --name functions --network coffeenchill-net -p 7120:80 -e AzureWebJobsStorage="<connection string>" -e StorageConnection="<same>" -e StaffDocsStorage="<real Azure connection string>" modestsnake/coffeenchill-functions:v1.0



\## Docker Hub

\- https://hub.docker.com/r/modestsnake/coffeenchill-functions

\- https://hub.docker.com/r/modestsnake/coffeenchill-azurite



\## Team Contributions

\- \[Name]: \[what they worked on]

\- \[Name]: \[what they worked on]



\## Demo Video

\[YouTube link]

