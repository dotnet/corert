BEGIN {
    print "V1.0 {";
    print "    global: _init; _fini;";
} 
{ 
	# entry starts with "_"?
	if (match($0, /^_.*/))
		gsub(/^_/,"", $0);
	print "        "$0 ";";
} 
END {
    print "    local: *;"
    print "};";
}
