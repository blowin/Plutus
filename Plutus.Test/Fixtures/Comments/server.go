//go:build !windows

// Package main is a fixture. The build directive above is a comment and is removed with it.
package main

import "fmt"

// Raw strings keep their comment markers.
var help = `
    usage: server -addr http://localhost:8080
    // this is data, not a comment
`

func main() {
    fmt.Println(help) // trailing comment
}
