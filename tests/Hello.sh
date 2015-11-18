
#!/usr/bin/env bash
if [[ $($1/$2.native | tr -d '\r') = "Hello world" ]]; then
    echo pass
    exit 0
else
    echo fail
    exit 1
fi
