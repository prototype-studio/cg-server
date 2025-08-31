set projectName=%1
set configuration=%2
set runtime=%3
set framework=%4
set outputDirectory="%projectName%/bin/%configuration%/publish/%runtime%"
set contentDirectory="%projectName%/CustomContent"
dotnet publish "%projectName%/%projectName%.csproj" -p:PublishSingleFile=true -p:DeleteExistingFiles=true -p:PublishTrimmed=true -c %configuration% -f %framework% --force -o %outputDirectory% -r %runtime% --self-contained 
Copy-Item -Path %contentDirectory%\* -Destination %outputDirectory% -Recurse -Force
powershell Compress-Archive -Path "%outputDirectory%\*" -DestinationPath "%outputDirectory%\%projectName%.zip" -Force
aws s3 cp "%outputDirectory%\%projectName%.zip" "s3://us-east-1.executables/%projectName%/%configuration%/%projectName%.zip"
aws deploy create-deployment --application-name cg-application --deployment-group-name cg-%configuration% --s3-location bucket=us-east-1.executables,key=%projectName%/%configuration%/%projectName%.zip,bundleType=zip --region us-east-1
pause