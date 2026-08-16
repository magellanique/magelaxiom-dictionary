# Publishing Magelaxiom

Run these from the repository root after authenticating GitHub CLI.

```powershell
gh auth login -h github.com
git branch -M main
git add .
git commit -m "Initial Magelaxiom release"
gh repo create magellanique/magelaxiom --public --source . --remote origin --push --description "A Magellanique open source offline English dictionary for Windows."
gh api --method POST /repos/magellanique/magelaxiom/pages -f build_type=workflow
gh release create v0.1.0 .\releases\Magelaxiom-v0.1.0-portable.zip .\dist\Magelaxiom.exe --title "Magelaxiom v0.1.0" --notes-file RELEASE_NOTES.md
```

After the first push, GitHub Actions should deploy the website from `docs/` to
GitHub Pages.
