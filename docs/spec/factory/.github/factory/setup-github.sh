#!/usr/bin/env bash
# One-time (idempotent) GitHub setup of the factory: labels, the Project with its Status options,
# and repository variables. Run by the human owner with their own `gh` login, from the repository root:
#
#   bash .github/factory/setup-github.sh
#
# It does NOT configure: rulesets and required checks, the merge queue, the GitHub App, environments
# and their secrets, the maintainers team. Those are the human setup checklist in the factory README.
set -euo pipefail

command -v gh >/dev/null || { echo "gh is required" >&2; exit 1; }
command -v jq >/dev/null || { echo "jq is required" >&2; exit 1; }

repo=$(gh repo view --json nameWithOwner --jq .nameWithOwner)
owner=${repo%%/*}
here=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
project_title="YAF factory"

echo "== Labels in $repo"
jq -c '.[]' "$here/labels.json" | while read -r label; do
  name=$(jq -r .name <<<"$label"); color=$(jq -r .color <<<"$label"); description=$(jq -r .description <<<"$label")
  gh label create "$name" --repo "$repo" --color "$color" --description "$description" --force >/dev/null
  echo "   $name"
done

echo "== Project \"$project_title\" of $owner"
number=$(gh project list --owner "$owner" --format json --limit 200 | jq -r --arg t "$project_title" '.projects[] | select(.title == $t) | .number' | head -n 1)
if [ -z "$number" ]; then
  number=$(gh project create --owner "$owner" --title "$project_title" --format json | jq -r .number)
  echo "   created #$number"
else
  echo "   exists as #$number"
fi
gh project link "$number" --owner "$owner" --repo "$repo" >/dev/null 2>&1 || true

# The Status field must offer exactly these options. A new project has Todo / In Progress / Done.
wanted='["Backlog","Ready","In progress","In review","Done"]'
field=$(gh project field-list "$number" --owner "$owner" --format json | jq -c '.fields[] | select(.name == "Status")')
have=$(jq -c '[.options[].name]' <<<"$field")
if [ "$(jq -c 'sort' <<<"$have")" != "$(jq -c 'sort' <<<"$wanted")" ]; then
  field_id=$(jq -r .id <<<"$field")
  options=$(jq -r 'map("{name: \"" + . + "\", color: GRAY, description: \"\"}") | join(", ")' <<<"$wanted")
  # Replacing the options clears the status of existing items; on a new project there are none.
  gh api graphql -f query="mutation { updateProjectV2Field(input: { fieldId: \"$field_id\", singleSelectOptions: [ $options ] }) { projectV2Field { ... on ProjectV2SingleSelectField { id } } } }" >/dev/null
  echo "   Status options set: Backlog, Ready, In progress, In review, Done"
else
  echo "   Status options already as wanted"
fi

echo "== Repository variables"
set_var() { gh variable set "$1" --repo "$repo" --body "$2" >/dev/null; echo "   $1=$2"; }
if ! gh variable get FACTORY_PAUSED --repo "$repo" >/dev/null 2>&1; then set_var FACTORY_PAUSED false; else echo "   FACTORY_PAUSED kept as $(gh variable get FACTORY_PAUSED --repo "$repo")"; fi
set_var FACTORY_PROJECT_OWNER "$owner"
set_var FACTORY_PROJECT_NUMBER "$number"

cat <<'NEXT'

Done. Still to do by hand (factory/README.md, "Human setup checklist"):
  - the GitHub App yaf-factory, its id in variable FACTORY_APP_ID, its key in environment factory-merge
  - environment factory-review with ANTHROPIC_API_KEY
  - the maintainers team and policy.json humans
  - rulesets for main and next with the required checks, CODEOWNERS review, merge queue
NEXT
