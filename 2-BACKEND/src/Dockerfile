# Utilisation de l'image de base dotnet SDK pour la phase de build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

WORKDIR /app

# Copier le fichier solution et les sources du projet
COPY MyDeezerStat.sln ./
COPY ./ ./

ARG HTTP_PROXY
ARG HTTPS_PROXY
ARG NO_PROXY

ENV HTTP_PROXY=$HTTP_PROXY
ENV HTTPS_PROXY=$HTTPS_PROXY
ENV NO_PROXY=$NO_PROXY

# Restaurer les dépendances et construire l'application
RUN dotnet restore
RUN dotnet publish -c Release -o /out

# Phase d'exécution de l'API avec l'image ASP.NET
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime

WORKDIR /app

# Copier les fichiers publiés depuis la phase de build
COPY --from=build /out .

# Exposer le port 80 pour l'API
EXPOSE 80

# Lancer l'application
ENTRYPOINT ["dotnet", "MyDeezerStats.API.dll"]