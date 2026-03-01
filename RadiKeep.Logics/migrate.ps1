$MigrationName = Read-Host -Prompt "マイグレーション名称を入力してください"

# dotnet tool update を実行
dotnet tool update --global dotnet-ef

# dotnet ef migrations add を実行
dotnet ef migrations add $MigrationName  -c RadiKeep.Logics.RdbContext.RadioDbContext -p ./RadiKeep.Logics.csproj -s ../RadiKeep/RadiKeep.csproj -o RdbContext/Migrations

Read-Host -Prompt "コマンドが完了しました。続行するにはEnterキーを押してください..."

