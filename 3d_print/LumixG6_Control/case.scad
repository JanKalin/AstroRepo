$fn = $preview ? 36 : 180;

// Board width
w_esp32_board = 17.8;

// Board length
l_esp32_board = 21;

// Board thickness
t_esp32_board = 1.6;

// CPU thickness
t_cpu = 3.3;

// USB width
w_esp32_usb = 9.5;

// USB Height
h_esp32_usb = 3.5;

// USB depth
d_esp32_usb = 7.3;

// USB overhang
overhang_esp32_usb = 1.3;

// USB tolerance
usb_tol = 0.25;

// Jack thread diameter
D_jack_thread = 6 + 0.25;

// Jack thread length
l_jack_thread = 4.2;

// Jack diameter
D_jack = 8;

// Jack length
l_jack = 22.1 - l_jack_thread;

// Wall
wall = overhang_esp32_usb - 0.1;

// Floor
flr = t_esp32_board;

// Ceiling
clng = 1.2;

// Inner length
l = 2.5*l_esp32_board;

// Inner width
w = w_esp32_board + 2*usb_tol;

// Inner height
h = D_jack + 1;

// Screw M
M = 2 + 0.1;

// Diameter of screw hole
D_screw = M - 0.5;

// Head width
d2 = 4;

// Screw length
l_screw = 5;

// ESP32
module esp32(){
  cube([l_esp32_board, w_esp32_board, t_esp32_board]);
  translate([-overhang_esp32_usb, w_esp32_board/2 - w_esp32_usb/2 - usb_tol, t_esp32_board]) cube([d_esp32_usb, w_esp32_usb + 2*usb_tol, h_esp32_usb + usb_tol]);
}
*translate([0, usb_tol, 0]) esp32();

// Box
module box(){
  difference(){
    union(){
      difference(){
        hull(){
          for( x = [0, l], y = [0, w] ){
            translate([x, y, 0]){
              translate([0, 0, -flr]) cylinder(d1=0, d2=2*wall, h=flr);
              cylinder(d=2*wall, h=h);
            }
          }
        }
        cube([l, w, h + 0.01]);
        translate([0, usb_tol, 0]) esp32();
        translate([l - 0.01, w/2, h/2]) rotate([0, 90, 0]) cylinder(d=D_jack_thread, h=wall + 0.02);
      }
      translate([l_esp32_board + usb_tol, 0, 0]) cube([2, w, 1]);
      for( xya = [[0, 0, 0], [l, 0, 90], [l, w, 180], [0, w, 270]] ){
        translate([xya[0], xya[1], 0]) rotate([0, 0, xya[2]]) hull(){
          translate([0, 0, h - l_screw + clng - 3*wall]) cylinder(d1=0, d2=wall, h=wall);
          translate([2*wall, 2*wall, h - l_screw + clng - wall]) cylinder(d1=0, d2=D_screw + 1.5, h=wall);
          translate([0, 0, h - l_screw + clng]) cylinder(d=wall, h=l_screw - clng);
          translate([2*wall, 2*wall, h - l_screw + clng]) cylinder(d1=0, d2=D_screw + 1.5, h=l_screw - clng);
        }
      }
    }
    for( xya = [[0, 0], [l, 0], [l, w], [0, w]] ){
      for( xya = [[0, 0, 0], [l, 0, 90], [l, w, 180], [0, w, 270]] ){
        translate([xya[0], xya[1], 0]) rotate([0, 0, xya[2]])
        translate([2*wall, 2*wall, h - l_screw + clng - 0.5])
        cylinder(d=D_screw, h=l_screw - clng + 0.5 + 0.01);
      }
    }
    translate([0, usb_tol, 0]) esp32();
  }
}

module cover(){
  difference(){
    hull(){
      for( x = [0, l], y = [0, w] ){
        translate([x, y, h]) cylinder(d1=2*wall, d2=0, h=clng);
      }
    }
    for( xya = [[0, 0], [l, 0], [l, w], [0, w]] ){
      for( xya = [[0, 0, 0], [l, 0, 90], [l, w, 180], [0, w, 270]] ){
        translate([xya[0], xya[1], h - 0.01]) rotate([0, 0, xya[2]])
        translate([2*wall, 2*wall, 0]){
          cylinder(d=M, h=clng + 0.02);
          translate([0, 0, clng - d2/2]) cylinder(d1=0, d2=d2, h=d2/2 + 0.02);
        }
      }
    }
  }
  translate([l_esp32_board*3/4, 0.1, t_esp32_board]) cube([l - l_esp32_board*6/4, wall, h - t_esp32_board]);
  translate([l_esp32_board*3/4, w - wall - 0.1, t_esp32_board]) cube([l - l_esp32_board*6/4, wall, h - t_esp32_board]);
}

box();
%cover();