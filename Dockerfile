FROM mcr.microsoft.com/dotnet/runtime:6.0 AS base

ENV TZ=Asia/Kuala_Lumpur
RUN ln -snf /usr/share/zoneinfo/$TZ /etc/localtime && echo $TZ > /etc/timezone

RUN apt update
RUN apt upgrade -y
RUN apt -y install wget curl unzip xvfb libxi6 libgconf-2-4
RUN apt-get install -y default-jdk 
RUN wget https://dl-ssl.google.com/linux/linux_signing_key.pub
RUN apt install -y gnupg
RUN apt-key add linux_signing_key.pub
RUN echo "deb [arch=amd64]  http://dl.google.com/linux/chrome/deb/ stable main" >> /etc/apt/sources.list.d/google-chrome.list
RUN apt-get -y update
RUN apt-get -y install google-chrome-stable

RUN wget https://chromedriver.storage.googleapis.com/95.0.4638.69/chromedriver_linux64.zip
RUN unzip chromedriver_linux64.zip
RUN mv chromedriver /usr/bin/chromedriver
RUN chmod +x /usr/bin/chromedriver

WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["MYMacbookPro2021-Checker-Bot.csproj", "."]
RUN dotnet restore "./MYMacbookPro2021-Checker-Bot.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "MYMacbookPro2021-Checker-Bot.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "MYMacbookPro2021-Checker-Bot.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MYMacbookPro2021-Checker-Bot.dll"]
