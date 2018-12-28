FROM microsoft/dotnet:2.1-aspnetcore-runtime AS base

# Install R
RUN apt-get update && \
	apt-get install -y dirmngr --install-recommends && \
	apt-get install -y software-properties-common apt-transport-https && \
    add-apt-repository 'deb https://cloud.r-project.org/bin/linux/debian stretch-cran35/' && \
	apt-get update && \
    apt-get install -y r-base --allow-unauthenticated && \
    apt-get install -y pandoc pandoc-citeproc && \
    apt-get install -y libxml2-dev libcurl4-openssl-dev && \
    apt-get clean && \
    rm -r /var/lib/apt/lists/*

# Install base packages
RUN Rscript -e "install.packages(c('rmarkdown', 'flexdashboard'), repos='http://cloud.r-project.org/')"

# Install common packages
RUN Rscript -e "install.packages(c('highcharter', 'dplyr', 'viridisLite', 'forecast', 'treemap'), repos='http://cloud.r-project.org/')"

EXPOSE 80
WORKDIR /app

FROM microsoft/dotnet:2.1-sdk AS build
WORKDIR /src
COPY . .
RUN dotnet restore -nowarn:msb3202,nu1503
RUN dotnet build -c Release -o /app

FROM build AS publish
RUN dotnet publish -c Release -o /app

FROM base AS final
WORKDIR /app
COPY --from=publish /app .
EXPOSE 80
CMD ["dotnet", "Rboard.Server.dll"]
