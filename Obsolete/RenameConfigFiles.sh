#/bin/bash

# MOVE THIS EXTERNAL - DB setup
apt install mariadb-server
systemctl start mariadb

mysql -u root

#	CREATE USER 'nexusforever' IDENTIFIED BY 'nexusforever';
#	GRANT ALL PRIVILEGES ON * . * TO 'nexusforever';

# check for rabbit install
apt list --installed | grep rabbit

# install
apt install rabbitmq-server -y

# start service
systemctl start rabbitmq-server

# Create Broker User
rabbitmqctl add_user nexusforever nexusforever
rabbitmqctl set_user_tags nexusforever administrator
rabbitmqctl set_permissions -p / nexusforever ".*" ".*" ".*"

# config files
cd /root/NexusForever/Source/NexusForever.AuthServer/bin/Debug/net10.0/
cp AuthServer.example.json AuthServer.json
cd /root/NexusForever/Source/NexusForever.StsServer/bin/Debug/net10.0/
cp StsServer.example.json StsServer.json
cd /root/NexusForever/Source/NexusForever.WorldServer/bin/Debug/net10.0/
cp WorldServer.example.json WorldServer.json
cd /root/NexusForever/Source/NexusForever.Server.ChatServer/bin/Debug/net10.0/
cp ChatServer.example.json ChatServer.json
cd /root/NexusForever/Source/NexusForever.Server.GroupServer/bin/Debug/net10.0/
cp GroupServer.example.json GroupServer.json
cd /root/NexusForever/Source/NexusForever.API.Character/bin/Debug/net10.0/
cp CharacterAPI.example.json CharacterAPI.json

# Database Migration - Entity Core Migrations
dotnet tool install dotnet-ef --tool-path /usr/bin

cd /root/NexusForever/Source/NexusForever.Server.WorldServer
dotnet-ef database update --context AuthContext
dotnet-ef database update --context CharacterContext
dotnet-ef database update --context WorldContext

cd /root/NexusForever/Source/NexusForever.Server.ChatServer
dotnet-ef database update

cd /root/NexusForever/Source/NexusForever.Server.GroupServer
dotnet-ef database update

# apply the NexusForever world database dumps to the world database
find -type f -name '*.sql' -exec sh -c 'mysql -u root nexus_forever_world < "{}"' \;