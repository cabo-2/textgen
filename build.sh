#!/bin/bash
dotnet build
dotnet publish -r linux-x64 -c Release --self-contained