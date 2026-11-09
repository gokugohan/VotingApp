<h1 align="center">Voting App Demo</h1>
<h3 align="center">"There is no one who loves pain itself, who seeks after it and wants to have it, simply because it is pain..."</h3>

- 📫 How to reach me **hchebre@gmail.com**

<h3 align="left">Connect with me:</h3>
<p align="left">
<a href="https://fb.com/helderchebre" target="blank"><img align="center" src="https://raw.githubusercontent.com/rahuldkjain/github-profile-readme-generator/master/src/images/icons/Social/facebook.svg" alt="helderchebre" height="30" width="40" /></a>
</p>


<p><img align="center" src="https://github-readme-stats.vercel.app/api/top-langs?username=gokugohan&show_icons=true&locale=en&layout=compact" alt="gokugohan" /></p>

<p><img align="center" src="https://github-readme-streak-stats.herokuapp.com/?user=gokugohan&" alt="gokugohan" /></p>

<img align="center" style="width:100%" src="https://user-images.githubusercontent.com/4695463/216550578-c733704f-d3a1-4bc7-a825-8d1ba8a4c92f.png"/>

## Running locally

Requires the .NET 8 SDK and SQL Server (Express or LocalDB).

Connection strings and the first admin account are **not** stored in the repo. Set them with user-secrets (development) or environment variables (production, e.g. `ConnectionStrings__DefaultConnection`):

```bash
cd VotingApp
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=.\SQLEXPRESS;Database=VotingApp;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
dotnet user-secrets set "Seed:AdminEmail" "admin@example.org"      # optional: creates the first admin
dotnet user-secrets set "Seed:AdminPassword" "<strong password>"
dotnet tool restore
dotnet ef database update
dotnet run
```

Public registration is disabled; admins create accounts under *Manage account → Create user*. The Admin role is granted every permission at start-up.

Run the tests with `dotnet test` from the repository root.
