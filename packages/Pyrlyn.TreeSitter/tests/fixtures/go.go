package main

import (
	"fmt"
	strs "strings"
)

// Shape has an area.
type Shape interface {
	Area() float64
}

// Square is a shape.
type Square struct {
	Side float64
}

// Area returns the area.
func (s Square) Area() float64 {
	return s.Side * s.Side
}

func main() {
	var s Shape = Square{Side: 2}
	fmt.Println(strs.ToUpper("area"), s.Area())
}
