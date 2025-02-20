# KLC-Ex 
Ex (Explorer) is an alternative frontend to Kaseya VSA 9.5's web browser Agents interface written in C#. It was functional up to VSA 9.5.20 however will not receive any further VSA testing/development.

The main reason this exists is because KLC-Proxy and KLC-Finch did not replicate the Agent browse/search feature that Kaseya Live Connect has and sometimes the VSA web interface did not perform well for me when I needed to rapidly switch between machines in different organisations or check agent procedure statuses.

## Usage
Typically, KLC-Ex is launched by KLC-Proxy rather than directly.

![Screenshot of KLC-Ex](/Resources/KLC-Ex-Blank.png?raw=true)

## Required other repos to build
- LibKaseya
- LibKaseyaAuth

## Required packages to build
- CredentialManagement.Standard
- Newtonsoft.Json
- nucs.JsonSettings
- RestSharp
