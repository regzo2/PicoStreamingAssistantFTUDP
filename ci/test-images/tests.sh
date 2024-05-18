#!/bin/

# project
cd /app/PicoStreamingAssistantFTUDP

# get the dependencies
git submodule update --init --recursive

# Restore dependencies
dotnet restore

# run the tests
dotnet test --logger "junit;LogFilePath=/app/PicoStreamingAssistantFTUDP/PicoStreamingAssistantFTTests/TestResults/dotnet-test-results.xml"