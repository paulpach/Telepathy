
docfx ./docs/docfx.json

Set-Variable -Name SOURCE_DIR "$PWD"
cd ..

Set-Variable -Name TEMP_REPO_DIR "telepathy-gh-pages"

echo "Removing temporary doc directory $TEMP_REPO_DIR"

if (Test-Path -Path $TEMP_REPO_DIR) { 
    Remove-Item -Path $TEMP_REPO_DIR -Recurse -Force
}

New-Item -ItemType directory -Path $TEMP_REPO_DIR

echo "Cloning the repo with the gh-pages branch"
git clone --branch=gh-pages https://github.com/paulpach/Telepathy.git $TEMP_REPO_DIR

echo "Clear repo directory"
cd $TEMP_REPO_DIR
git rm -r *

echo "Copy documentation into the repo"
cp -r $SOURCE_DIR/docs/_site/* .

echo "Push the new docs to the remote branch"
git add . -A
git commit -m "Update generated documentation"
git push origin gh-pages
