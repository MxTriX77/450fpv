# Spec Delta: repo-hygiene

## ADDED Requirements

### Requirement: Commit message format
The commit-message hook SHALL reject a commit whose subject is longer than 72 characters, whose subject does not read `type(scope): summary` or `type: summary` with a type from `feat`, `fix`, `perf`, `refactor`, `test`, `docs`, `chore`, `art`, whose summary starts with an upper-case letter, or whose message carries any non-empty line after the subject (a body, a trailer or a co-author line). Git's own merge commits SHALL pass, whatever `fmt-merge-msg` writes (branch, branches, remote-tracking branch, tag, commit, pull request). The subject is the first non-blank line of the message, whatever it starts with; a message without one SHALL be rejected. `#` lines after the subject are git's editor template and are ignored.

#### Scenario: Long subject is rejected
- **WHEN** a commit message's first line has 73 or more characters
- **THEN** the commit is rejected, naming the rule and the length

#### Scenario: Wrong shape is rejected
- **WHEN** the subject reads `Added the thing` or `feature(x): thing` or `fix(x): Thing`
- **THEN** the commit is rejected

#### Scenario: Body or trailer is rejected
- **WHEN** the message has a second non-empty line, including a `Co-Authored-By:` trailer
- **THEN** the commit is rejected

#### Scenario: Conforming subject passes
- **WHEN** the subject reads `fix(review): keep the bar in frame` with nothing after it
- **THEN** the format rule passes

#### Scenario: Git merge commit passes
- **WHEN** the message is one git wrote itself for a merge of a branch, a remote-tracking branch, a tag, a commit or a pull request
- **THEN** the format rule passes

#### Scenario: Comment-like subject is not a bypass
- **WHEN** the message's first non-blank line starts with `#`, or the message is empty
- **THEN** the commit is rejected
